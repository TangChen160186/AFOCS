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
/// 沿指定耦合直线轴扫描并返回角度（AGenData[817]）。
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
    public double Angle { get; set; }

    // ========== 配置属性 ==========

    [DisplayName("轴")]
    [ItemsSource(typeof(CouplingXYZAxisItemsSource))]
    [Category("配置")]
    public EAxis Axis
    {
        get;
        set => Set(ref field, value);
    } = EAxis.CouplingLX;

    [DisplayName("采样间距(脉冲)")]
    [Category("配置")]
    public double SamplingInterval
    {
        get;
        set => Set(ref field, value);
    } = 10;

    [DisplayName("起始距离(脉冲)")]
    [Category("配置")]
    public double StartDistance
    {
        get;
        set => Set(ref field, value);
    } = -1024;

    [DisplayName("停止距离(脉冲)")]
    [Category("配置")]
    public double StopDistance
    {
        get;
        set => Set(ref field, value);
    } = 1024;

    [DisplayName("最大扫描速度(脉冲/s)")]
    [Category("配置")]
    public double MaxScanSpeed
    {
        get;
        set => Set(ref field, value);
    } = 204800;

    [DisplayName("最大回归速度(脉冲/s)")]
    [Category("配置")]
    public double MaxReturnSpeed
    {
        get;
        set => Set(ref field, value);
    } = 20480;

    [DisplayName("间距宽度(mm)")]
    [Category("配置")]
    public double SpacingWidth
    {
        get;
        set => Set(ref field, value);
    } = 0.02;

    [DisplayName("采集通道")]
    [Category("配置")]
    public int AcquireChannel
    {
        get;
        set => Set(ref field, value);
    } = 1;

    [DisplayName("曲线通道数")]
    [Description("实时发送到曲线图显示的通道数量（按通道号升序取前 N 个），默认 3")]
    [Category("配置")]
    public int CurveChannelCount
    {
        get;
        set => Set(ref field, value);
    } = 3;

    // ========== 执行 ==========

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        if (!context.TryGetValue("WorkPos", out var workPosObj) || workPosObj is not WorkPos station)
            throw new InvalidOperationException("流程上下文缺少工位（WorkPos），请确认已连接入口节点并设置工位");

        if (!Axis.IsAkribisAxis())
            throw new InvalidOperationException($"{Axis.GetDescription()}: TX单轴耦合仅支持雅克贝斯耦合直线轴");

        var (instanceName, akAxis) = Axis.ToAkribis(station);
        var instances = akribisMotions.ToDictionary(m => m.GetType().Name);
        if (!instances.TryGetValue(instanceName, out var motion))
            throw new InvalidOperationException($"{Axis.GetDescription()}: 未找到控制器 {instanceName}");

        if (CurveChannelCount <= 0)
            throw new InvalidOperationException("曲线通道数必须大于 0");

        // 记录扫描起点，用于还原各采样点的绝对位置
        int startPos = GetPosition(motion, akAxis);

        var args = new SingleAxisCouplingArgs
        {
            // AkribisAxisId.X/Y/Z -> 0/1/2 -> 控制器 A/B/C 轴
            Axis = (int)akAxis,
            SamplingInterval = SamplingInterval,
            StartDistance = StartDistance,
            StopDistance = StopDistance,
            MaxScanSpeed = MaxScanSpeed,
            MaxReturnSpeed = MaxReturnSpeed,
            SpacingWidth = SpacingWidth,
            AcquireChannel = AcquireChannel,
        };

        var result = await motion.SingleAxisCouplingAsync(args);
        if (!result.IsSuccess || result.Data == null)
            throw new InvalidOperationException($"{Axis.GetDescription()}: {result.Message}");

        // 扫描完成后把各通道光功率曲线发布给曲线面板
        PublishCurve(station, startPos, result.Data);

        Angle = result.Data.Angle;
        logger.Information("TX单轴耦合：{Axis} 角度={Angle:F4}°", Axis.GetDescription(), Angle);

        return new Dictionary<string, object?> { ["Angle"] = Angle };
    }

    // ==================== 曲线发布 ====================

    private void PublishCurve(WorkPos station, int startPos, AkribisCouplingResult result)
    {
        _ = eventAggregator.PublishOnUIThreadAsync(new CouplingSampleMessage
        {
            WorkPos = station,
            Type = CouplingSampleType.Start,
            ValueLabel = "功率",
        });

        try
        {
            var channels = result.ChannelPower?
                .OrderBy(kv => kv.Key)
                .Take(CurveChannelCount)
                .ToList();

            if (channels is { Count: > 0 })
            {
                int count = channels[0].Value.Count;
                for (int i = 0; i < count; i++)
                {
                    _ = eventAggregator.PublishOnUIThreadAsync(new CouplingSampleMessage
                    {
                        WorkPos = station,
                        Type = CouplingSampleType.Sample,
                        Position = (int)Math.Round(startPos + StartDistance + i * SamplingInterval),
                        ChannelValues = channels.ToDictionary(kv => kv.Key, kv => kv.Value[i]),
                    });
                }
            }
        }
        finally
        {
            _ = eventAggregator.PublishOnUIThreadAsync(new CouplingSampleMessage
            {
                WorkPos = station,
                Type = CouplingSampleType.End,
            });
        }
    }

    private static int GetPosition(IAkribisMotion motion, AkribisAxisId axis) => axis switch
    {
        AkribisAxisId.X => motion.PositionX,
        AkribisAxisId.Y => motion.PositionY,
        AkribisAxisId.Z => motion.PositionZ,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };
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
