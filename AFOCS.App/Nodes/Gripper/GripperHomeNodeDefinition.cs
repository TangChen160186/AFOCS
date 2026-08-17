using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Devices.Gripper;
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
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class GripperHomeNodeDefinition(
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

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        // 工位由入口节点传入，未提供时直接报错，避免对错误工位的夹爪回零
        if (!context.TryGetValue("WorkPos", out var workPosObj) || workPosObj is not WorkPos station)
            throw new InvalidOperationException("流程上下文缺少工位（WorkPos），请确认已连接入口节点并设置工位");

        var gripper = grippers.FirstOrDefault(e => e.WorkPos == station && e.GripperType == Coupling);
        if (gripper == null)
            throw new InvalidOperationException($"未找到工位 {station} 耦合 {Coupling} 的夹爪实例，请确认已配置夹爪实例");

        var result = await gripper.HomeAsync();
        if (!result.IsSuccess)
        {
            var errInfo = $"夹爪回零失败: {result.Message}";
            logger.Error(errInfo);
            throw new InvalidOperationException(errInfo);
        }

        return new Dictionary<string, object?>();
    }
}
