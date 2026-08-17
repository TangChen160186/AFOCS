using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.App.Models;
using AFOCS.Devices.AkribrisMotion;
using AFOCS.Devices.BusAxisDevice;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Motion;

/// <summary>
/// 轴相对运动节点：选择轴，按给定距离做相对（增量）运动。
/// 工位由入口节点传入（context["WorkPos"]），总线轴使用定长运动（posiMode=0 相对模式），
/// 雅克贝斯轴使用相对运动接口。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.MoveAxisRelative", "轴相对运动", "运动")]
[CategoryOrder("基础", 0), 
 CategoryOrder("配置", 1), 
 CategoryOrder("输入", 2), 
 CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class MoveAxisRelativeNodeDefinition(IBusAxisDevice busAxisDevice,
    [ImportMany] IEnumerable<IAkribisMotion> akribisMotions)
    : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("轴")]
    [ItemsSource(typeof(AxisItemsSource))]
    [Category("配置")]
    public EAxis Axis
    {
        get;
        set => Set(ref field, value);
    } = EAxis.CouplingLThetaX;

    [DisplayName("距离")]
    [NodePort("Distance", "距离", NodePortType.Double, true)]
    [Category("输入")]
    public double Distance
    {
        get;
        set => Set(ref field, value);
    } = 100000.0;

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        if (!context.TryGetValue("WorkPos", out var workPosObj) || workPosObj is not WorkPos station)
            throw new InvalidOperationException("流程上下文缺少工位（WorkPos），请确认已连接入口节点并设置工位");

        string? error;
        if (Axis.IsBusAxis())
        {
            var busId = Axis.ToBusAxisId(station);
            var moveResult = await busAxisDevice.MovePmoveAsync(busId, Distance, posiMode: 0);
            error = moveResult.IsSuccess ? null : moveResult.Message;
        }
        else if (Axis.IsAkribisAxis())
        {
            var (instanceName, akAxis) = Axis.ToAkribis(station);
            var akribisInstances = akribisMotions.ToDictionary(m => m.GetType().Name);
            if (!akribisInstances.TryGetValue(instanceName, out var motion))
            {
                error = $"{Axis.GetDescription()}: 未找到控制器 {instanceName}";
            }
            else
            {
                var akResult = await motion.MoveRelativeAsync(akAxis, (int)Distance);
                error = akResult.IsSuccess ? null : akResult.Message;
            }
        }
        else
        {
            error = $"{Axis.GetDescription()}: 未知轴类型";
        }

        if (error != null)
        {
            throw new InvalidOperationException($"运动失败: {error}");
        }
        return new Dictionary<string, object?>();
    }
}