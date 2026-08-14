using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Text.Json.Serialization;
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
/// RX 单轴耦合节点：沿指定耦合直线轴扫描，每次移动后读取 ISP 板 RSP 值，
/// 记录两个通道 RSP 峰值对应的位置，以两峰值位置差为斜边、通道物理间隙为邻边，
/// 计算倾斜角度。由原来的「RX调平耦合」整合而来。
///   hypotenuse = |pos1 - pos2| / PulsePerUm   (um)
///   angle      = arccos(GapUm / hypotenuse) × 180 / π   (度)
/// 扫描范围：以当前位置为中心，负方向 NegativeLengthPulse、正方向 PositiveLengthPulse（均为脉冲）；
/// 扫描结束（含异常）移回原位。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.RxSingleAxisCoupling", "RX单轴耦合", "耦合")]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class RxSingleAxisCouplingNodeDefinition(
    IIspBoardDevice ispBoard,
    ILogger logger,
    [ImportMany] IEnumerable<IAkribisMotion> akribisMotions)
    : NodeDefinitionBase, IExecutableNode
{
    // ========== 输出端口 ==========

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("Angle", "角度", NodePortType.Double, false)]
    [Category("输出")]
    public double Angle { get; set; }

    // ========== 输入端口 ==========

    [DisplayName("通道间隙(um)")]
    [NodePort("GapUm", "通道间隙", NodePortType.Double, true)]
    [Category("输入")]
    public double GapUm { get; set; }

    // ========== 配置属性 ==========

    [DisplayName("轴")]
    [ItemsSource(typeof(CouplingXYZAxisItemsSource))]
    [Category("配置")]
    public EAxis Axis
    {
        get;
        set => Set(ref field, value);
    } = EAxis.CouplingLY;

    [DisplayName("通道1")]
    [Category("配置")]
    public int Channel1
    {
        get;
        set => Set(ref field, value);
    }

    [DisplayName("通道2")]
    [Category("配置")]
    public int Channel2
    {
        get;
        set => Set(ref field, value);
    } = 1;

    [DisplayName("负方向长度(脉冲)")]
    [Category("配置")]
    public int NegativeLengthPulse
    {
        get;
        set => Set(ref field, value);
    } = 20480;

    [DisplayName("正方向长度(脉冲)")]
    [Category("配置")]
    public int PositiveLengthPulse
    {
        get;
        set => Set(ref field, value);
    } = 20480;

    [DisplayName("步长(脉冲)")]
    [Category("配置")]
    public int StepPulse
    {
        get;
        set => Set(ref field, value);
    } = 2048;

    [DisplayName("延时(ms)")]
    [Category("配置")]
    public int DelayMs
    {
        get;
        set => Set(ref field, value);
    } = 50;

    [DisplayName("脉冲当量(脉冲/um)")]
    [Category("配置")]
    public double PulsePerUm
    {
        get;
        set => Set(ref field, value);
    } = 204.8;

    // ========== 执行 ==========

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        // 工位由入口节点传入，未提供时直接报错，避免误用错误工位的轴
        if (!context.TryGetValue("WorkPos", out var workPosObj) || workPosObj is not WorkPos station)
            throw new InvalidOperationException("流程上下文缺少工位（WorkPos），请确认已连接入口节点并设置工位");

        if (!Axis.IsAkribisAxis())
            throw new InvalidOperationException($"{Axis.GetDescription()}: RX单轴耦合仅支持雅克贝斯耦合直线轴");

        var (instanceName, akAxis) = Axis.ToAkribis(station);
        var akribisInstances = akribisMotions.ToDictionary(m => m.GetType().Name);
        if (!akribisInstances.TryGetValue(instanceName, out var motion))
            throw new InvalidOperationException($"{Axis.GetDescription()}: 未找到控制器 {instanceName}");

        if (NegativeLengthPulse < 0 || PositiveLengthPulse < 0)
            throw new InvalidOperationException("扫描长度不能为负");
        if (StepPulse <= 0)
            throw new InvalidOperationException("步长必须大于 0");
        if (Channel1 == Channel2)
            throw new InvalidOperationException("通道1 与 通道2 不能相同");
        if (GapUm <= 0)
            throw new InvalidOperationException("通道间隙必须大于 0");

        int startPos = GetPosition(motion, akAxis);
        var samples = new List<(int Position, double Rsp1, double Rsp2)>();

        try
        {
            // 以当前位置为中心，负方向扫 NegativeLengthPulse，正方向扫 PositiveLengthPulse
            for (int pos = startPos - NegativeLengthPulse; pos <= startPos + PositiveLengthPulse; pos += StepPulse)
            {
                var moveResult = await motion.MoveAbsAsync(akAxis, pos);
                if (!moveResult.IsSuccess)
                    throw new InvalidOperationException($"{Axis.GetDescription()} 移动到 {pos} 失败: {moveResult.Message}");

                if (DelayMs > 0)
                    await Task.Delay(DelayMs);

                double rsp1 = 0, rsp2 = 0;
                var readResult = await ispBoard.ReadRspAsync(station);
                if (readResult.IsSuccess)
                {
                    foreach (var ch in readResult.Data)
                    {
                        if (ch.Channel == Channel1) rsp1 = ch.RspValue;
                        else if (ch.Channel == Channel2) rsp2 = ch.RspValue;
                    }
                }

                samples.Add((pos, rsp1, rsp2));
            }
        }
        finally
        {
            // 扫描结束（含异常）移回原位
            await motion.MoveAbsAsync(akAxis, startPos);
        }

        var peak1 = samples.OrderByDescending(s => s.Rsp1).First();
        var peak2 = samples.OrderByDescending(s => s.Rsp2).First();

        // 斜边 = 两通道峰值位置差的物理距离(um)
        double hypotenuseUm = Math.Abs(peak1.Position - peak2.Position) / PulsePerUm;
        if (hypotenuseUm <= 0 || GapUm > hypotenuseUm)
            throw new InvalidOperationException(
                $"RX单轴耦合：通道间隙 {GapUm}um 大于斜边 {hypotenuseUm:F3}um（arccos 定义域错误），请检查扫描范围/步长/通道");

        // θ = arccos(邻边/斜边)
        double angleDeg = Math.Acos(GapUm / hypotenuseUm) * 180.0 / Math.PI;

        Angle = angleDeg;
        logger.Information(
            "RX单轴耦合：ch1峰值@{P1}, ch2峰值@{P2}, 斜边={H:F3}um, 角度={A:F4}°",
            peak1.Position, peak2.Position, hypotenuseUm, angleDeg);

        return new Dictionary<string, object?> { ["Angle"] = angleDeg };
    }

    private static int GetPosition(IAkribisMotion motion, AkribisAxisId axis) => axis switch
    {
        AkribisAxisId.X => motion.PositionX,
        AkribisAxisId.Y => motion.PositionY,
        AkribisAxisId.Z => motion.PositionZ,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };
}
