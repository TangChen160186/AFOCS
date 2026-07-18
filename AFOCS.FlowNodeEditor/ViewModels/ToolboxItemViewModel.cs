using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.ViewModels
{
    public class ToolboxItemViewModel
    {
        public string Category { get; }
        public string DisplayName { get; }
        public Uri? IconSource { get; }
        public string TypeId { get; }
        public INodeDefinition Definition { get; }

        public ToolboxItemViewModel(INodeDefinition definition)
        {
            Definition = definition;
            Category = NodeDefinitionHelper.GetCategory(definition);
            DisplayName = NodeDefinitionHelper.GetDisplayName(definition);
            TypeId = NodeDefinitionHelper.GetTypeId(definition);
            IconSource = NodeDefinitionHelper.GetIconSource(definition);
        }

        public NodeViewModel CreateNodeViewModel() => new(Definition);
    }
}