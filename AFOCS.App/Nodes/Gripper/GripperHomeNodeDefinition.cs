using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Devices;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Gripper;

/// <summary>
/// 夹爪回零节点：按工位（入口节点传入）与耦合位置选择夹爪实例，执行回零。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.GripperHome", "夹爪回零", "回零")]
[method: ImportingConstructor]
public class GripperHomeNodeDefinition(
    ILogger logger,
    [ImportMany] IEnumerable<ISmcGripper> grippers) : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("耦合")]
    [ItemsSource(typeof(GripperCouplingItemsSource))]
    public GripperCoupling Coupling
    {
        get;
        set => Set(ref field, value);
    } = GripperCoupling.L;

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        // 工位由入口节点传入，未提供时直接报错，避免对错误工位的夹爪回零
        if (!context.TryGetValue("WorkPos", out var workPosObj) || workPosObj is not WorkPos station)
            throw new InvalidOperationException("流程上下文缺少工位（WorkPos），请确认已连接入口节点并设置工位");

        var prefix = station == WorkPos.Left ? "Left" : "Right";
        var instanceName = $"{prefix}Coupling{Coupling}Gripper";

        var gripperInstances = grippers.ToDictionary(g => g.GetType().Name);
        if (!gripperInstances.TryGetValue(instanceName, out var gripper))
            throw new InvalidOperationException($"未找到夹爪 {instanceName}，请检查设备配置");

        var result = await gripper.HomeAsync();
        if (!result.IsSuccess)
        {
            var errInfo = $"夹爪回零失败: {result.Message}";
            logger.Error(errInfo);
            throw new InvalidOperationException(errInfo);
        }

        logger.Information("夹爪 {Gripper} 回零完成", instanceName);
        return new Dictionary<string, object?>();
    }
}
