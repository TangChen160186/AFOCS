using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Devices;
using AFOCS.Devices.Enums;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure.Extensions;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.IO;

/// <summary>
/// 读取IO状态节点：读取指定输入信号当前状态（逻辑值，已考虑有效电平），通过 Bool 端口输出，
/// 供条件判断等节点使用。为纯数据节点，不参与执行流。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.IoState", "读取IO状态", "设备", HasExecutionInput = false, HasExecutionOutput = false)]
[method: ImportingConstructor]
public class IoStateNodeDefinition(
    IIODevice ioDevice,
    ILogger logger) : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("IO输入信号")]
    [ItemsSource(typeof(IoInputItemsSource))]
    public AllInputs Signal
    {
        get;
        set => Set(ref field, value);
    }

    [Browsable(false)]
    [NodePort("State", "状态", NodePortType.Bool, false)]
    public bool State
    {
        get;
        set => Set(ref field, value);
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        State = ioDevice.GetState(Signal);
        logger.Information("读取IO状态: {Signal} = {State}", Signal, State);

        return Task.FromResult(new Dictionary<string, object?>
        {
            ["State"] = State,
        });
    }
}

public class IoInputItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        foreach (var signal in Enum.GetValues<AllInputs>())
            items.Add(signal, signal.GetDescription());
        return items;
    }
}
