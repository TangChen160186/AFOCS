using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;

namespace AFOCS.FlowNodeEditor.Nodes;

[NodeDefinition("Builtin.Divide", "除法", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
[Export(typeof(INodeDefinition))]
public class DivideNodeDefinition : NodeDefinitionBase, IExecutableNode
{

    [NodePort("A", "A", NodePortType.Double, true)]
    public double A
    {
        get;
        set => Set(ref field, value);
    }

    [NodePort("B", "B", NodePortType.Double, true)]
    public double B
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
        Result = B == 0 ? double.NaN : A / B;
        return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
    }
}