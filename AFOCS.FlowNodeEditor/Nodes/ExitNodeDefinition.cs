using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;

namespace AFOCS.FlowNodeEditor.Nodes;

[NodeDefinition("Builtin.Exit", "出口", "流程", HasExecutionInput = true, HasExecutionOutput = false)]
[Export(typeof(INodeDefinition))]
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class ExitNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        return Task.FromResult(new Dictionary<string, object?>());
    }
}