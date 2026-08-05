using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.App.Models;
using AFOCS.Devices;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Motion;

/// <summary>
/// 轴回零节点：选择轴（工位由入口节点传入 context["WorkPos"]），执行回零。
/// 总线轴使用 MoveHomeAsync，雅克贝斯轴使用 HomeAsync。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.MoveAxisHome", "轴回零", "回零")]
[method: ImportingConstructor]
public class MoveAxisHomeNodeDefinition(
    IBusAxisDevice busAxisDevice,
    ILogger logger,
    [ImportMany] IEnumerable<IAkribisMotion> akribisMotions)
    : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("轴")]
    [ItemsSource(typeof(AxisItemsSource))]
    public EAxis Axis
    {
        get;
        set => Set(ref field, value);
    } = EAxis.CouplingLThetaX;

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        // 工位由入口节点传入，未提供时直接报错，避免对错误工位的轴回零
        if (!context.TryGetValue("WorkPos", out var workPosObj) || workPosObj is not WorkPos station)
            throw new InvalidOperationException("流程上下文缺少工位（WorkPos），请确认已连接入口节点并设置工位");

        string? error;
        if (Axis.IsBusAxis())
        {
            var busId = Axis.ToBusAxisId(station);
            var result = await busAxisDevice.MoveHomeAsync(busId);
            error = result.IsSuccess ? null : result.Message;
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
                var result = await motion.HomeAsync(akAxis);
                error = result.IsSuccess ? null : result.Message;
            }
        }
        else
        {
            error = $"{Axis.GetDescription()}: 未知轴类型";
        }

        if (error != null)
        {
            var errInfo = $"回零失败: {error}";
            logger.Error(errInfo);
            throw new InvalidOperationException(errInfo);
        }

        logger.Information("轴 {Axis}（{Station}）回零完成", Axis.GetDescription(), station);
        return new Dictionary<string, object?>();
    }
}
