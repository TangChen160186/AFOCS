using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.ViewModels;

public class ToolboxItemViewModel(INodeDefinition template)
{
    public string Category { get; } = NodeDefinitionHelper.GetCategory(template);
    public string DisplayName { get; } = NodeDefinitionHelper.GetDisplayName(template);
    public Uri? IconSource { get; } = NodeDefinitionHelper.GetIconSource(template);
    public string TypeId { get; } = NodeDefinitionHelper.GetTypeId(template);
    public INodeDefinition Template { get; } = template;

    public NodeViewModel CreateNodeViewModel()
    {
        var clonedDefinition = NodeDefinitionHelper.Clone(Template);
        return new NodeViewModel(clonedDefinition);
    }
}