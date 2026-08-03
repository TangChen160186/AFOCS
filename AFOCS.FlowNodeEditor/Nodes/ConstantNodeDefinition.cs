using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;

namespace AFOCS.FlowNodeEditor.Nodes;

[NodeDefinition("Builtin.Constant", "常量", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
[Export(typeof(INodeDefinition))]
public class ConstantNodeDefinition : NodeDefinitionBase, IExecutableNode
{

    public double Value
    {
        get;
        set => Set(ref field, value);
    }

    [Browsable(false)]
    [NodePort("Result", "结果", NodePortType.Double, false)]
    public double Result
    {
        get;
        set => Set(ref field, value);
    }
    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        Result = Value;
        return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
    }
}