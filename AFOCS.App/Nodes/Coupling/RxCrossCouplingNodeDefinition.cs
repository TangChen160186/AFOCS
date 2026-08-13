using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.App.Models;
using AFOCS.Devices.AkribrisMotion;
using AFOCS.Devices.IspBoard;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Coupling;

/// <summary>
/// RX 十字耦合节点：对两个耦合轴（默认 X/Y）分别扫描，记录各自两个通道 RSP 峰值位置，
/// 然后把每个轴移动到「两个通道峰值位置的中间」。
/// 扫描范围：以当前轴位置为中心，负方向扫负长度、正方向扫正长度（均为脉冲）；
/// 步长与延时两个轴共用；每个轴独立选择两个通道。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.RxCrossCoupling", "RX十字耦合", "耦合")]
[method: ImportingConstructor]
public class RxCrossCouplingNodeDefinition(
    IIspBoardDevice ispBoard,
    ILogger logger,
    [ImportMany] IEnumerable<IAkribisMotion> akribisMotions)
    : NodeDefinitionBase, IExecutableNode
{
    // ========== 轴1 ==========

    [DisplayName("轴1")]
    [ItemsSource(typeof(CouplingXYAxisItemsSource))]
    public EAxis Axis1
    {
        get;
        set => Set(ref field, value);
    } = EAxis.CouplingLX;

    [DisplayName("轴1负方向长度(脉冲)")]
    public int Axis1NegativePulse
    {
        get;
        set => Set(ref field, value);
    } = 20480;

    [DisplayName("轴1正方向长度(脉冲)")]
    public int Axis1PositivePulse
    {
        get;
        set => Set(ref field, value);
    } = 20480;

    [DisplayName("轴1通道1")]
    public int Axis1Channel1
    {
        get;
        set => Set(ref field, value);
    }

    [DisplayName("轴1通道2")]
    public int Axis1Channel2
    {
        get;
        set => Set(ref field, value);
    } = 1;

    // ========== 轴2 ==========

    [DisplayName("轴2")]
    [ItemsSource(typeof(CouplingXYAxisItemsSource))]
    public EAxis Axis2
    {
        get;
        set => Set(ref field, value);
    } = EAxis.CouplingLY;

    [DisplayName("轴2负方向长度(脉冲)")]
    public int Axis2NegativePulse
    {
        get;
        set => Set(ref field, value);
    } = 20480;

    [DisplayName("轴2正方向长度(脉冲)")]
    public int Axis2PositivePulse
    {
        get;
        set => Set(ref field, value);
    } = 20480;

    [DisplayName("轴2通道1")]
    public int Axis2Channel1
    {
        get;
        set => Set(ref field, value);
    }

    [DisplayName("轴2通道2")]
    public int Axis2Channel2
    {
        get;
        set => Set(ref field, value);
    } = 1;

    // ========== 共用参数 ==========

    [DisplayName("步长(脉冲)")]
    public int StepPulse
    {
        get;
        set => Set(ref field, value);
    } = 2048;

    [DisplayName("延时(ms)")]
    public int DelayMs
    {
        get;
        set => Set(ref field, value);
    } = 50;

    // ========== 执行 ==========

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        // 工位由入口节点传入
        if (!context.TryGetValue("WorkPos", out var workPosObj) || workPosObj is not WorkPos station)
            throw new InvalidOperationException("流程上下文缺少工位（WorkPos），请确认已连接入口节点并设置工位");

        if (!Axis1.IsAkribisAxis() || !Axis2.IsAkribisAxis())
            throw new InvalidOperationException("RX十字耦合仅支持耦合直线轴（X/Y/Z）");
        if (StepPulse <= 0)
            throw new InvalidOperationException("步长必须大于 0");
        if (Axis1NegativePulse < 0 || Axis1PositivePulse < 0 ||
            Axis2NegativePulse < 0 || Axis2PositivePulse < 0)
            throw new InvalidOperationException("扫描长度不能为负");
        if (Axis1Channel1 == Axis1Channel2)
            throw new InvalidOperationException("轴1 的通道1 与 通道2 不能相同");
        if (Axis2Channel1 == Axis2Channel2)
            throw new InvalidOperationException("轴2 的通道1 与 通道2 不能相同");

        var instances = akribisMotions.ToDictionary(m => m.GetType().Name);

        var (instance1, akAxis1) = Axis1.ToAkribis(station);
        var motion1 = ResolveMotion(instances, instance1, Axis1);

        var (instance2, akAxis2) = Axis2.ToAkribis(station);
        var motion2 = ResolveMotion(instances, instance2, Axis2);

        await ScanAndCenterAsync(motion1, akAxis1, station, Axis1NegativePulse, Axis1PositivePulse,
            Axis1Channel1, Axis1Channel2, Axis1.GetDescription());
        await ScanAndCenterAsync(motion2, akAxis2, station, Axis2NegativePulse, Axis2PositivePulse,
            Axis2Channel1, Axis2Channel2, Axis2.GetDescription());

        return new Dictionary<string, object?>();
    }

    // ========== 辅助 ==========

    private IAkribisMotion ResolveMotion(
        Dictionary<string, IAkribisMotion> instances, string instanceName, EAxis axis)
    {
        if (!instances.TryGetValue(instanceName, out var motion))
            throw new InvalidOperationException($"{axis.GetDescription()}: 未找到控制器 {instanceName}");
        return motion;
    }

    private static int GetPosition(IAkribisMotion motion, AkribisAxisId axis) => axis switch
    {
        AkribisAxisId.X => motion.PositionX,
        AkribisAxisId.Y => motion.PositionY,
        AkribisAxisId.Z => motion.PositionZ,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };

    private async Task<int> ScanAndCenterAsync(
        IAkribisMotion motion, AkribisAxisId akAxis, WorkPos station,
        int negativePulse, int positivePulse, int ch1, int ch2, string axisDesc)
    {
        int startPos = GetPosition(motion, akAxis);
        var samples = new List<(int Position, double Rsp1, double Rsp2)>();

        for (int pos = startPos - negativePulse; pos <= startPos + positivePulse; pos += StepPulse)
        {
            var moveResult = await motion.MoveAbsAsync(akAxis, pos);
            if (!moveResult.IsSuccess)
                throw new InvalidOperationException($"{axisDesc} 移动到 {pos} 失败: {moveResult.Message}");

            if (DelayMs > 0)
                await Task.Delay(DelayMs);

            double rsp1 = 0, rsp2 = 0;
            var readResult = await ispBoard.ReadRspAsync(station);
            if (readResult.IsSuccess)
            {
                foreach (var ch in readResult.Data)
                {
                    if (ch.Channel == ch1) rsp1 = ch.RspValue;
                    else if (ch.Channel == ch2) rsp2 = ch.RspValue;
                }
            }

            samples.Add((pos, rsp1, rsp2));
        }

        var peak1 = samples.OrderByDescending(s => s.Rsp1).First();
        var peak2 = samples.OrderByDescending(s => s.Rsp2).First();

        int center = (int)Math.Round((peak1.Position + peak2.Position) / 2.0);
        var centerMove = await motion.MoveAbsAsync(akAxis, center);
        if (!centerMove.IsSuccess)
            throw new InvalidOperationException($"{axisDesc} 移动到中心 {center} 失败: {centerMove.Message}");

        logger.Information("RX十字耦合：{Axis} ch1峰值@{P1}, ch2峰值@{P2}, 中心={C}",
            axisDesc, peak1.Position, peak2.Position, center);

        return center;
    }
}

/// <summary>RX十字耦合可选择的轴（耦合直线 X/Y）</summary>
public class CouplingXYAxisItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        foreach (var axis in new[] { EAxis.CouplingLX, EAxis.CouplingLY, EAxis.CouplingRX, EAxis.CouplingRY })
            items.Add(axis, axis.GetDescription());
        return items;
    }
}
