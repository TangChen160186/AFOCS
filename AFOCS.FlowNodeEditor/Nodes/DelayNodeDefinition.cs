using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.FlowNodeEditor.Nodes;

[NodeDefinition("Builtin.Delay", "延时", "基础",HasExecutionInput = true, HasExecutionOutput = true)]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[Export(typeof(INodeDefinition))]
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class DelayNodeDefinition : NodeDefinitionBase, ICancellableExecutableNode
{
    [DisplayName("延时(ms)")]
    [Category("配置")]
    public int DelayMs 
    { 
        get; 
        set => Set(ref field, value);
    }

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context, CancellationToken cancellationToken)
    {
        await Task.Delay(DelayMs, cancellationToken);
        return new Dictionary<string, object?>();
    }

    Task<Dictionary<string, object?>> IExecutableNode.ExecuteAsync(Dictionary<string, object?> context)
        => ExecuteAsync(context, CancellationToken.None);
}