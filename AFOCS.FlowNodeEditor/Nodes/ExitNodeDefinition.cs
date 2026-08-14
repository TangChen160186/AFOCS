using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.FlowNodeEditor.Nodes;

[NodeDefinition("Builtin.Exit", "出口", "流程", HasExecutionInput = true, HasExecutionOutput = false)]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
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