//using System.ComponentModel;
//using System.ComponentModel.Composition;
//using System.Text.Json.Serialization;
//using AFOCS.App.Models;
//using AFOCS.Devices.AkribrisMotion;
//using AFOCS.FlowNodeEditor.Models;
//using AFOCS.FlowNodeEditor.Services;
//using AFOCS.Infrastructure;
//using AFOCS.Infrastructure.Extensions;
//using Caliburn.Micro;
//using Serilog;
//using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

//namespace AFOCS.App.Nodes.Coupling;

///// <summary>
///// TX 螺旋耦合节点：调用雅克贝斯控制器螺旋找光（AGenData 协议），
///// 双轴螺旋扫描，返回各通道最大光功率。
///// 工位由入口节点传入（context["WorkPos"]）。
///// </summary>
//[Export]
//[Export(typeof(INodeDefinition))]
//[PartCreationPolicy(CreationPolicy.NonShared)]
//[NodeDefinition("App.TxSpiralCoupling", "TX螺旋耦合", "耦合")]
//[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
//[method: ImportingConstructor]
//public class TxSpiralCouplingNodeDefinition(
//    ILogger logger,
//    IEventAggregator eventAggregator,
//    [ImportMany] IEnumerable<IAkribisMotion> akribisMotions)
//    : NodeDefinitionBase, IExecutableNode
//{
//    // ========== 输出端口 ==========

//    [JsonIgnore]
//    [ReadOnly(true)]
//    [NodePort("MaxPower", "最大光功率", NodePortType.Double, false)]
//    [Category("输出")]
//    public double MaxPower { get; set; }

//    // ========== 配置属性 ==========

//    [DisplayName("轴1")]
//    [ItemsSource(typeof(CouplingXYAxisItemsSource))]
//    [Category("配置")]
//    public EAxis Axis1
//    {
//        get;
//        set => Set(ref field, value);
//    } = EAxis.CouplingLX;

//    [DisplayName("轴2")]
//    [ItemsSource(typeof(CouplingXYAxisItemsSource))]
//    [Category("配置")]
//    public EAxis Axis2
//    {
//        get;
//        set => Set(ref field, value);
//    } = EAxis.CouplingLY;

//    [DisplayName("螺距")]
//    [Category("配置")]
//    public double Pitch
//    {
//        get;
//        set => Set(ref field, value);
//    } = 1.0;

//    [DisplayName("最大扫描半径(脉冲)")]
//    [Category("配置")]
//    public double MaxScanRadius
//    {
//        get;
//        set => Set(ref field, value);
//    } = 500;

//    [DisplayName("最大扫描速度(脉冲/s)")]
//    [Category("配置")]
//    public double MaxScanSpeed
//    {
//        get;
//        set => Set(ref field, value);
//    } = 204800;

//    [DisplayName("最大回归速度(脉冲/s)")]
//    [Category("配置")]
//    public double MaxReturnSpeed
//    {
//        get;
//        set => Set(ref field, value);
//    } = 20480;

//    [DisplayName("采集通道")]
//    [Category("配置")]
//    public int AcquireChannel
//    {
//        get;
//        set => Set(ref field, value);
//    } = 1;

//    // ========== 执行 ==========

//    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
//    {
//        if (!context.TryGetValue("WorkPos", out var workPosObj) || workPosObj is not WorkPos station)
//            throw new InvalidOperationException("流程上下文缺少工位（WorkPos），请确认已连接入口节点并设置工位");

//        if (!Axis1.IsAkribisAxis() || !Axis2.IsAkribisAxis())
//            throw new InvalidOperationException("TX螺旋耦合仅支持雅克贝斯耦合直线轴");
//        if (Axis1 == Axis2)
//            throw new InvalidOperationException("轴1 与 轴2 不能相同");

//        var (instance1, akAxis1) = Axis1.ToAkribis(station);
//        var (instance2, akAxis2) = Axis2.ToAkribis(station);
//        if (instance1 != instance2)
//            throw new InvalidOperationException(
//                $"轴1({Axis1.GetDescription()}) 与 轴2({Axis2.GetDescription()}) 必须属于同一控制器");

//        var instances = akribisMotions.ToDictionary(m => m.GetType().Name);
//        if (!instances.TryGetValue(instance1, out var motion))
//            throw new InvalidOperationException($"{Axis1.GetDescription()}: 未找到控制器 {instance1}");

//        var args = new SpiralCouplingArgs
//        {
//            Axis1 = (int)akAxis1,
//            Axis2 = (int)akAxis2,
//            Pitch = Pitch,
//            MaxScanRadius = MaxScanRadius,
//            MaxScanSpeed = MaxScanSpeed,
//            MaxReturnSpeed = MaxReturnSpeed,
//            AcquireChannel = AcquireChannel,
//        };

//        var result = await motion.SpiralCouplingAsync(args);
//        if (!result.IsSuccess || result.Data == null)
//            throw new InvalidOperationException($"{Axis1.GetDescription()}/{Axis2.GetDescription()}: {result.Message}");

//        // 螺旋为二维扫描，曲线 X 轴使用采样序号
//        PublishCurve(station, result.Data);

//        double maxPower = 0;
//        var channelPower = result.Data.ChannelPower;
//        if (channelPower != null && channelPower.Count > 0)
//            maxPower = channelPower.Values.SelectMany(v => v).DefaultIfEmpty(0).Max();

//        MaxPower = maxPower;
//        logger.Information("TX螺旋耦合：{A1}/{A2} 最大光功率={P:F4}",
//            Axis1.GetDescription(), Axis2.GetDescription(), MaxPower);

//        return new Dictionary<string, object?> { ["MaxPower"] = MaxPower };
//    }

//    // ==================== 曲线发布 ====================

//    private void PublishCurve(WorkPos station, AkribisCouplingResult result)
//    {
//        _ = eventAggregator.PublishOnUIThreadAsync(new CouplingSampleMessage
//        {
//            WorkPos = station,
//            Source = CouplingSource.Tx,
//            Type = CouplingSampleType.Start,
//            ValueLabel = "功率",
//        });

//        try
//        {
//            var channels = result.ChannelPower?
//                .OrderBy(kv => kv.Key)
//                .ToList();

//            if (channels is { Count: > 0 })
//            {
//                int count = channels[0].Value.Count;
//                for (int i = 0; i < count; i++)
//                {
//                    _ = eventAggregator.PublishOnUIThreadAsync(new CouplingSampleMessage
//                    {
//                        WorkPos = station,
//                        Source = CouplingSource.Tx,
//                        Type = CouplingSampleType.Sample,
//                        Position = i,
//                        ChannelValues = channels.ToDictionary(kv => kv.Key, kv => kv.Value[i]),
//                    });
//                }
//            }
//        }
//        finally
//        {
//            _ = eventAggregator.PublishOnUIThreadAsync(new CouplingSampleMessage
//            {
//                WorkPos = station,
//                Source = CouplingSource.Tx,
//                Type = CouplingSampleType.End,
//            });
//        }
//    }
//}

///// <summary>耦合直线 X/Y 轴（用于双轴/螺旋节点）</summary>
//public class CouplingXYAxisItemsSource : IItemsSource
//{
//    public ItemCollection GetValues()
//    {
//        var items = new ItemCollection();
//        foreach (var axis in new[] { EAxis.CouplingLX, EAxis.CouplingLY, EAxis.CouplingRX, EAxis.CouplingRY })
//            items.Add(axis, axis.GetDescription());
//        return items;
//    }
//}
