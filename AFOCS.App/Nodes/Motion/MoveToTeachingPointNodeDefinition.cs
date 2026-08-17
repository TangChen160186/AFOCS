using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.App.Models;
using AFOCS.Devices.AkribrisMotion;
using AFOCS.Devices.BusAxisDevice;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Motion;

/// <summary>
/// 运动到固定示教点节点：按示教点配置中的轴位置，将各轴运动到目标绝对位置。
/// 通过示教点 Guid 定位（名称可重复、可改名，Guid 才是稳定标识）。
/// 总线轴使用定长运动（绝对模式），雅克贝斯轴使用绝对运动。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.MoveToTeachingPoint", "运动到示教点", "运动")]
[CategoryOrder("基础", 0), 
 CategoryOrder("配置", 1), 
 CategoryOrder("输入", 2), 
 CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class MoveToTeachingPointNodeDefinition(IConfigService configService, IBusAxisDevice busAxisDevice, 
    [ImportMany] IEnumerable<IAkribisMotion> akdAkribisMotion)
    : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("示教点")]
    [ItemsSource(typeof(TeachingPointItemsSource))]
    [Category("配置")]
    public Guid PointId
    {
        get; 
        set => Set(ref field, value);
    }

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        var akribisInstances = akdAkribisMotion.ToDictionary(m => m.GetType().Name);
            
        var config = await configService.LoadAsync<TeachingPointsConfig>();
        var point = config?.Points.FirstOrDefault(p => p.Id == PointId);
        if (point == null)
            throw new InvalidOperationException($"未找到示教点（Id: {PointId}），请重新选择");

        var axisKeys = point.AxisKeys;
        var positions = point.AxisPositions;
        if (axisKeys.Count == 0 || positions.Count == 0)
            throw new InvalidOperationException($"示教点 \"{point.Name}\" 没有关联轴");

        var station = point.Station;
        var tasks = axisKeys
            .Where(positions.ContainsKey)
            .Select(axis => MoveSingleAxisAsync(axis, positions[axis], station, busAxisDevice, akribisInstances))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var errors = results.Where(r => r != null).Cast<string>().ToList();

        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"运动失败: {string.Join("; ", errors)}");
        }
        return new Dictionary<string, object?>();
    }

    private static async Task<string?> MoveSingleAxisAsync(
        EAxis axis,
        double targetPos,
        WorkPos station,
        IBusAxisDevice busAxisDevice,
        IReadOnlyDictionary<string, IAkribisMotion> akribisInstances)
    {
        try
        {
            if (axis.IsBusAxis())
            {
                var busId = axis.ToBusAxisId(station);
                var moveResult = await busAxisDevice.MovePmoveAsync(busId, targetPos, posiMode: 1);
                return moveResult.IsSuccess ? null : $"{axis.GetDescription()}: {moveResult.Message}";
            }

            if (axis.IsAkribisAxis())
            {
                var (instanceName, akAxis) = axis.ToAkribis(station);
                if (!akribisInstances.TryGetValue(instanceName, out var motion))
                    return $"{axis.GetDescription()}: 未找到控制器 {instanceName}";

                var akResult = await motion.MoveAbsAsync(akAxis, (int)targetPos);
                return akResult.IsSuccess ? null : $"{axis.GetDescription()}: {akResult.Message}";
            }

            return $"{axis.GetDescription()}: 未知轴类型";
        }
        catch (Exception ex)
        {
            return $"{axis.GetDescription()}: {ex.Message}";
        }
    }
}