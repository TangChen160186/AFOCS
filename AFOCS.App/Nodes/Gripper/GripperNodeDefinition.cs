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
    public GripperType Coupling
    {
        get;
        set => Set(ref field, value);
    } = GripperType.LeftCouplingGripper;

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
        if (!context.TryGetValue("WorkPos", out var workPosObj) || workPosObj is not WorkPos station)
            throw new InvalidOperationException("流程上下文缺少工位（WorkPos），请确认已连接入口节点并设置工位");

        var gripper = grippers.FirstOrDefault(e=>e.WorkPos == station && e.GripperType == Coupling);
        if(gripper==null)
            throw new InvalidOperationException($"未找到工位 {station} 耦合 {Coupling} 的夹爪实例，请确认已配置夹爪实例");

        var result = await gripper.MoveAsync(Speed, Position);
        if (!result.IsSuccess)
        {
            var errInfo = $"夹爪运动失败: {result.Message}";
            logger.Error(errInfo);
            throw new InvalidOperationException(errInfo);
        }
        return new Dictionary<string, object?>();
    }
}