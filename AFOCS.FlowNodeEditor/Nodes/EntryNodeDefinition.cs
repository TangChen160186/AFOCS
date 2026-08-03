using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;

namespace AFOCS.FlowNodeEditor.Nodes;


[NodeDefinition("Builtin.Entry", "入口", "流程", HasExecutionInput = false, HasExecutionOutput = true)]
[Export(typeof(INodeDefinition))]
public class EntryNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("优先级")]
    public int Priority
    {
        get;
        set => Set(ref field, value);
    }
    [DisplayName("工位")]
    public WorkPos Workpos
    {
        get;
        set => Set(ref field, value);
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        var result = new Dictionary<string, object?>
        {
            ["WorkPos"] = Workpos,
        };
        return Task.FromResult(result);
    }
}