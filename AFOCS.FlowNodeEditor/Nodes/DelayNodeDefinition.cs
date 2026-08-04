using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;

namespace AFOCS.FlowNodeEditor.Nodes;

[NodeDefinition("Builtin.Delay", "延时", "基础",HasExecutionInput = true, HasExecutionOutput = true)]
[Export(typeof(INodeDefinition))]
public class DelayNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("延时(ms)")]
    public int DelayMs 
    { 
        get; 
        set => Set(ref field, value);
    }

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        await Task.Delay(DelayMs);
        return new Dictionary<string, object?>();
    }
}