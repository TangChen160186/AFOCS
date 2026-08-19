using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Text.Json.Serialization;
using AFOCS.App.Models;
using AFOCS.Devices.AkribrisMotion;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Caliburn.Micro;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Coupling;

/// <summary>
/// TX 单轴耦合节点：调用雅克贝斯控制器单轴找光（AGenData 协议），
/// 沿指定耦合直线轴扫描并采集 4 个通道光功率，用两通道光功率峰值位置差
/// 与通道物理间隙计算倾斜角度（与 RX 单轴耦合一致）。
///   delta = (peak1 - peak2) / PulsePerUm   (um，带方向)
///   angle = atan2(delta, GapUm × |ch1 − ch2|) × 180 / π   (度，带符号)
/// 输出：Angle（倾斜角度，度，带符号）、Center（两通道峰值位置的中心，脉冲）。
/// 界面参数与 RX 单轴耦合一致，内部转换为控制器单轴找光参数：
/// StartDistance=-NegativeLengthPulse、StopDistance=+PositiveLengthPulse、
/// SamplingInterval=StepPulse、MaxScanSpeed=MaxReturnSpeed=Speed、
/// SpacingWidthUm=GapUm、AcquireChannel=0b1111（采集全部 4 通道）。
/// 工位由入口节点传入（context["WorkPos"]）。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.TxSingleAxisCoupling", "TX单轴耦合", "耦合")]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class TxSingleAxisCouplingNodeDefinition(
    ILogger logger,
    IEventAggregator eventAggregator,
    [ImportMany] IEnumerable<IAkribisMotion> akribisMotions)
    : NodeDefinitionBase, IExecutableNode
{
    // ========== 输出端口 ==========

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("Angle", "角度", NodePortType.Double, false)]
    [Category("输出")]
    public double Angle
    {
        get;
        set => Set(ref field, value);
    }

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("Center", "中心位置", NodePortType.Double, false)]
    [Category("输出")]
    public double Center
    {
        get;
        set => Set(ref field, value);
    }

    // ========== 输入端口 ==========

    [DisplayName("相邻通道间隙(um)")]
    [Description("相邻两个通道之间的物理间距")]
    [Category("输入")]
    public double GapUm
    {
        get;
        set => Set(ref field, value);
    } = 200;

    // ========== 配置属性 ==========

    [DisplayName("轴")]
    [ItemsSource(typeof(CouplingXYZAxisItemsSource))]
    [Category("配置")]
    public EAxis Axis
    {
        get;
        set => Set(ref field, value);
    } = EAxis.CouplingLX;

    [DisplayName("通道1")]
    [Category("配置")]
    public int Channel1
    {
        get;
        set => Set(ref field, value);
    } = 1;

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
    } = 1024;

    [DisplayName("正方向长度(脉冲)")]
    [Category("配置")]
    public int PositiveLengthPulse
    {
        get;
        set => Set(ref field, value);
    } = 1024;

    [DisplayName("步长(脉冲)")]
    [Category("配置")]
    public int StepPulse
    {
        get;
        set => Set(ref field, value);
    } = 10;

    [DisplayName("速度(脉冲/s)")]
    [Description("耦合扫描速度，同时用于扫描与回归")]
    [Category("配置")]
    public int Speed
    {
        get;
        set => Set(ref field, value);
    } = 204800;

    private const double PulsePerUm = 204.8;

    // ========== 执行 ==========

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        // 工位由入口节点传入，未提供时直接报错，避免误用错误工位的轴
        if (!context.TryGetValue("WorkPos", out var workPosObj) || workPosObj is not WorkPos station)
            throw new InvalidOperationException("流程上下文缺少工位（WorkPos），请确认已连接入口节点并设置工位");

        if (!Axis.IsAkribisAxis())
            throw new InvalidOperationException($"{Axis.GetDescription()}: TX单轴耦合仅支持雅克贝斯耦合直线轴");

        var (instanceName, akAxis) = Axis.ToAkribis(station);
        var instances = akribisMotions.ToDictionary(m => m.GetType().Name);
        if (!instances.TryGetValue(instanceName, out var motion))
            throw new InvalidOperationException($"{Axis.GetDescription()}: 未找到控制器 {instanceName}");

        if (NegativeLengthPulse < 0 || PositiveLengthPulse < 0)
            throw new InvalidOperationException("扫描长度不能为负");
        if (StepPulse <= 0)
            throw new InvalidOperationException("步长必须大于 0");
        if (Channel1 < 1 || Channel1 > 4 || Channel2 < 1 || Channel2 > 4)
            throw new InvalidOperationException("通道1/通道2 必须在 1~4 之间");
        if (Channel1 == Channel2)
            throw new InvalidOperationException("通道1 与 通道2 不能相同");
        if (GapUm <= 0)
            throw new InvalidOperationException("相邻通道间隙必须大于 0");

        var args = new SingleAxisCouplingArgs
        {
            // AkribisAxisId.X/Y/Z -> 0/1/2 -> 控制器 A/B/C 轴
            Axis = (int)akAxis,
            // 采样间距 = 步长
            SamplingInterval = StepPulse,
            // 以当前位置为中心，负方向 NegativeLengthPulse、正方向 PositiveLengthPulse
            StartDistance = -NegativeLengthPulse,
            StopDistance = PositiveLengthPulse,
            // 扫描与回归使用同一耦合速度
            MaxScanSpeed = Speed,
            MaxReturnSpeed = Speed,
            // 相邻通道间隙(um) 直接作为控制器间距宽度，内部按 20um=4096 脉冲换算
            SpacingWidthUm = GapUm,
            // 0b1111：采集全部 4 个通道
            AcquireChannel = 0b1111,
        };

        var result = await motion.SingleAxisCouplingAsync(args);
        if (!result.IsSuccess || result.Data == null)
            throw new InvalidOperationException($"{Axis.GetDescription()}: {result.Message}");

        // 各通道光功率峰值对应位置坐标（脉冲，AGenData[704-707]）
        var peaks = result.Data.PeakPositions;
        if (!peaks.TryGetValue(Channel1, out var peak1) || !peaks.TryGetValue(Channel2, out var peak2))
            throw new InvalidOperationException($"{Axis.GetDescription()}: 未获取到通道{Channel1}/{Channel2}的峰值位置");

        // 扫描完成后把各通道光功率曲线发布给曲线面板
        PublishCurve(station, result.Data);

        // 峰位差（带方向）：两通道峰值位置之差换算为物理距离(um)
        double deltaUm = (peak1 - peak2) / PulsePerUm;

        // 所选两通道的实际物理间距 = 相邻通道间隙 × 通道序号间隔
        double channelGapUm = GapUm * Math.Abs(Channel1 - Channel2);

        // θ = atan2(峰位差, 两通道间距)，带符号反映倾斜方向；共峰时角度为 0（表面垂直于扫描轴）
        double angleDeg = Math.Atan2(deltaUm, channelGapUm) * 180.0 / Math.PI;

        // 两通道峰值位置的中心（脉冲）
        double centerPulse = (peak1 + peak2) / 2.0;

        Angle = angleDeg;
        Center = centerPulse;
        logger.Information(
            "TX单轴耦合：{Axis} ch{Ch1}峰值@{P1}, ch{Ch2}峰值@{P2}, 峰位差={D:F3}um, 两通道间距={Gap}um, 角度={A:F4}°, 中心={C:F1}脉冲",
            Axis.GetDescription(), Channel1, peak1, Channel2, peak2, deltaUm, channelGapUm, angleDeg, centerPulse);

        return new Dictionary<string, object?> { ["Angle"] = angleDeg, ["Center"] = centerPulse };
    }

    // ==================== 曲线发布 ====================

    private void PublishCurve(WorkPos station, AkribisCouplingResult result)
    {
        _ = eventAggregator.PublishOnUIThreadAsync(new CouplingSampleMessage
        {
            WorkPos = station,
            Source = CouplingSource.Tx,
            Type = CouplingSampleType.Start,
            ValueLabel = "功率",
        });

        try
        {
            var channels = result.ChannelPower?
                .OrderBy(kv => kv.Key)
                .ToDictionary(kv => kv.Key, kv => (IReadOnlyList<double>)kv.Value);

            if (channels is { Count: > 0 })
            {
                // 数据一次性返回，批量发布全部采样点（X 轴用控制器实际记录的轴位置坐标）
                _ = eventAggregator.PublishOnUIThreadAsync(new CouplingSampleMessage
                {
                    WorkPos = station,
                    Source = CouplingSource.Tx,
                    Type = CouplingSampleType.Batch,
                    Positions = result.AxisPositions,
                    ChannelSeries = channels,
                });
            }
        }
        finally
        {
            _ = eventAggregator.PublishOnUIThreadAsync(new CouplingSampleMessage
            {
                WorkPos = station,
                Source = CouplingSource.Tx,
                Type = CouplingSampleType.End,
            });
        }
    }
}

/// <summary>TX单轴耦合可选择的轴（耦合直线 X/Y/Z）</summary>
public class CouplingXYZAxisItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        foreach (var axis in new[]
                 {
                     EAxis.CouplingLX, EAxis.CouplingLY, EAxis.CouplingLZ,
                     EAxis.CouplingRX, EAxis.CouplingRY, EAxis.CouplingRZ,
                 })
            items.Add(axis, axis.GetDescription());
        return items;
    }
}
