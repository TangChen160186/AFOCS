using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Text.Json.Serialization;
using AFOCS.App.Models;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Motion;

/// <summary>
/// 示教点坐标节点：选择一个示教点并指定轴，输出该轴在示教点中的坐标值给下游节点。
/// 通过示教点 Guid 定位（名称可重复、可改名，Guid 才是稳定标识）。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.TeachingPointValue", "示教点坐标", "运动")]
[method: ImportingConstructor]
public class TeachingPointValueNodeDefinition(IConfigService configService, ILogger logger)
    : NodeDefinitionBase, IExecutableNode
{
    // ========== 输出端口 ==========

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("Value", "坐标值", NodePortType.Double, false)]
    public double Value
    {
        get;
        set => Set(ref field, value);
    }
// ========== 配置属性 ==========

        [DisplayName("示教点")]
    [ItemsSource(typeof(TeachingPointItemsSource))]
    public Guid PointId
    {
        get;
        set => Set(ref field, value);
    }

    [DisplayName("轴")]
    [ItemsSource(typeof(AxisItemsSource))]
    public EAxis Axis
    {
        get;
        set => Set(ref field, value);
    } = EAxis.CouplingLZ;

    // ========== 执行 ==========

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        var config = await configService.LoadAsync<TeachingPointsConfig>();
        var point = config?.Points.FirstOrDefault(p => p.Id == PointId);
        if (point == null)
            throw new InvalidOperationException($"示教点坐标节点：未找到示教点（Id: {PointId}），请重新选择");

        if (!point.AxisPositions.TryGetValue(Axis, out var value))
            throw new InvalidOperationException(
                $"示教点坐标节点：示教点 \"{point.Name}\" 未包含轴 {Axis.GetDescription()} 的坐标");

        Value = value;
        logger.Information("示教点坐标节点：{Point} 的 {Axis} = {Value}", point.Name, Axis.GetDescription(), value);

        return new Dictionary<string, object?> { ["Value"] = value };
    }
}
