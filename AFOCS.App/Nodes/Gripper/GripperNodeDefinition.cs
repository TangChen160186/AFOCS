using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Devices.Gripper;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Gripper;

public enum GripperCoupling
{
    [Description("左耦合")]
    L,

    [Description("右耦合")]
    R,
}

/// <summary>
/// 控制夹爪节点：按工位（入口节点传入）与耦合位置选择夹爪实例，以指定速度运动到目标位置。
/// 位置单位 0.01mm（与 SMC 电夹爪一致），夹紧/松开由目标位置决定。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.Gripper", "控制夹爪", "运动")]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class GripperNodeDefinition(
    ILogger logger,
    [ImportMany] IEnumerable<IGripper> grippers) : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("耦合")]
    [ItemsSource(typeof(GripperCouplingItemsSource))]
    [Category("配置")]
    public GripperCoupling Coupling
    {
        get;
        set => Set(ref field, value);
    } = GripperCoupling.L;

    [DisplayName("速度")]
    [Category("配置")]
    public ushort Speed
    {
        get;
        set => Set(ref field, value);
    } = 100;

    [DisplayName("位置(0.01mm)")]
    [Category("配置")]
    public ushort Position
    {
        get;
        set => Set(ref field, value);
    }

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        // 工位由入口节点传入，未提供时直接报错，避免夹取到错误工位的夹爪
        if (!context.TryGetValue("WorkPos", out var workPosObj) || workPosObj is not WorkPos station)
            throw new InvalidOperationException("流程上下文缺少工位（WorkPos），请确认已连接入口节点并设置工位");

        var prefix = station == WorkPos.Left ? "Left" : "Right";
        var instanceName = $"{prefix}Coupling{Coupling}Gripper";

        var gripperInstances = grippers.ToDictionary(g => g.GetType().Name);
        if (!gripperInstances.TryGetValue(instanceName, out var gripper))
            throw new InvalidOperationException($"未找到夹爪 {instanceName}，请检查设备配置");

        var result = await gripper.MoveAsync(Speed, Position);
        if (!result.IsSuccess)
        {
            var errInfo = $"夹爪运动失败: {result.Message}";
            logger.Error(errInfo);
            throw new InvalidOperationException(errInfo);
        }

        logger.Information("夹爪 {Gripper} 运动到位置 {Position}，速度 {Speed}",
            instanceName, Position, Speed);
        return new Dictionary<string, object?>();
    }
}

public class GripperCouplingItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        foreach (var coupling in Enum.GetValues<GripperCoupling>())
            items.Add(coupling, coupling.GetDescription());
        return items;
    }
}
