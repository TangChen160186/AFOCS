using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Text.Json.Serialization;
using AFOCS.App.Models;
using AFOCS.Devices.AkribrisMotion;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
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
[method: ImportingConstructor]
public class TxSingleAxisCouplingNodeDefinition(
    ILogger logger,
    [ImportMany] IEnumerable<IAkribisMotion> akribisMotions)
    : NodeDefinitionBase, IExecutableNode
{
    // ========== 输出端口 ==========

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("Angle", "角度", NodePortType.Double, false)]
    public double Angle { get; set; }

    // ========== 配置属性 ==========

    [DisplayName("轴")]
    [ItemsSource(typeof(CouplingXYZAxisItemsSource))]
    public EAxis Axis
    {
        get;
        set => Set(ref field, value);
    } = EAxis.CouplingLX;

    [DisplayName("采样间距(脉冲)")]
    public double SamplingInterval
    {
        get;
        set => Set(ref field, value);
    } = 10;

    [DisplayName("起始距离(脉冲)")]
    public double StartDistance
    {
        get;
        set => Set(ref field, value);
    } = -1024;

    [DisplayName("停止距离(脉冲)")]
    public double StopDistance
    {
        get;
        set => Set(ref field, value);
    } = 1024;

    [DisplayName("最大扫描速度(脉冲/s)")]
    public double MaxScanSpeed
    {
        get;
        set => Set(ref field, value);
    } = 204800;

    [DisplayName("最大回归速度(脉冲/s)")]
    public double MaxReturnSpeed
    {
        get;
        set => Set(ref field, value);
    } = 20480;

    [DisplayName("间距宽度(mm)")]
    public double SpacingWidth
    {
        get;
        set => Set(ref field, value);
    } = 0.02;

    [DisplayName("采集通道")]
    public int AcquireChannel
    {
        get;
        set => Set(ref field, value);
    } = 1;

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

        Angle = result.Data.Angle;
        logger.Information("TX单轴耦合：{Axis} 角度={Angle:F4}°", Axis.GetDescription(), Angle);

        return new Dictionary<string, object?> { ["Angle"] = Angle };
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
