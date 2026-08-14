using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.FlowNodeEditor.Nodes;

[NodeDefinition("Builtin.Add", "加法", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[Export(typeof(INodeDefinition))]
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class AddNodeDefinition : NodeDefinitionBase, IExecutableNode
{
   
    [NodePort("A", "A", NodePortType.Double, true)]
    [Category("输入")]
    public double A
    {
        get;
        set => Set(ref field, value);
    }

    [NodePort("B", "B", NodePortType.Double, true)]
    [Category("输入")]
    public double B
    {
        get;
        set => Set(ref field, value);
    }

    [Browsable(false)]

    [NodePort("Result", "结果", NodePortType.Double, false)]
    [Category("输出")]
    public double Result
    {
        get;
        set => Set(ref field, value);
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        Result = A + B;
        return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
    }
}