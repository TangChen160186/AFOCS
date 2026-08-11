using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;

namespace AFOCS.FlowNodeEditor.Nodes;

/// <summary>
/// 入口节点：流程执行的起点。工位由编辑器工具栏的全局选择器控制，不再在此节点单独设置。
/// </summary>
[NodeDefinition("Builtin.Entry", "入口", "流程", HasExecutionInput = false, HasExecutionOutput = true)]
[Export(typeof(INodeDefinition))]
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class EntryNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("优先级")]
    public int Priority
    {
        get;
        set => Set(ref field, value);
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        // WorkPos 由 FlowExecutor 在 context["WorkPos"] 中预注入，此处不再覆盖
        return Task.FromResult(new Dictionary<string, object?>());
    }
}
