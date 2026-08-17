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
/// 记录两个通道 RSP 峰值对应的位置，以两峰值位置差（带方向）与通道物理间隙
/// 计算倾斜角度。由原来的「RX调平耦合」整合而来。
///   delta      = (pos1 - pos2) / PulsePerUm   (um，带方向)
///   angle      = atan2(delta, GapUm × |ch1 − ch2|) × 180 / π   (度，带符号)
/// 输出：Angle（倾斜角度，度，带符号）、Center（两通道峰值位置的中心，脉冲）。
/// 扫描范围：以当前位置为中心，负方向 NegativeLengthPulse、正方向 PositiveLengthPulse（均为脉冲）；
/// 扫描结束（含异常）移回原位。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.RxSingleAxisCoupling", "RX单轴耦合", "耦合")]
[CategoryOrder("基础", 0),
 CategoryOrder("配置", 1), 
 CategoryOrder("输入", 2),
 CategoryOrder("输出", 3)]
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

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("Center", "中心位置", NodePortType.Double, false)]
    [Category("输出")]
    public double Center { get; set; }

    // ========== 输入端口 ==========

    [DisplayName("相邻通道间隙(um)")]
    [Description("相邻两个通道之间的物理间距")]
    [NodePort("GapUm", "相邻通道间隙", NodePortType.Double, true)]
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
    } = 3;

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
    } = 18;

    private const double PulsePerUm = 204.8;

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
            throw new InvalidOperationException("相邻通道间隙必须大于 0");

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

        // 峰位差（带方向）：两通道峰值位置之差换算为物理距离(um)
        double deltaUm = (peak1.Position - peak2.Position) / PulsePerUm;

        // 所选两通道的实际物理间距 = 相邻通道间隙 × 通道序号间隔
        double channelGapUm = GapUm * Math.Abs(Channel1 - Channel2);

        // θ = atan2(峰位差, 两通道间距)，带符号反映倾斜方向；共峰时角度为 0（表面垂直于扫描轴）
        double angleDeg = Math.Atan2(deltaUm, channelGapUm) * 180.0 / Math.PI;

        // 两通道峰值位置的中心（脉冲）
        double centerPulse = (peak1.Position + peak2.Position) / 2.0;

        Angle = angleDeg;
        Center = centerPulse;
        logger.Information(
            "RX单轴耦合：ch1峰值@{P1}, ch2峰值@{P2}, 峰位差={D:F3}um, 两通道间距={Gap}um, 角度={A:F4}°, 中心={C:F1}脉冲",
            peak1.Position, peak2.Position, deltaUm, channelGapUm, angleDeg, centerPulse);

        return new Dictionary<string, object?> { ["Angle"] = angleDeg, ["Center"] = centerPulse };
    }

    private static int GetPosition(IAkribisMotion motion, AkribisAxisId axis) => axis switch
    {
        AkribisAxisId.X => motion.PositionX,
        AkribisAxisId.Y => motion.PositionY,
        AkribisAxisId.Z => motion.PositionZ,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };
}
