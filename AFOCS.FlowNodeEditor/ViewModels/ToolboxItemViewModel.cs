using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.ViewModels
{
    public class ToolboxItemViewModel
    {
        public string Category { get; }
        public string DisplayName { get; }
        public Uri? IconSource { get; }
        public string TypeId { get; }
        public INodeDefinition Template { get; }

        public ToolboxItemViewModel(INodeDefinition template)
        {
            Template = template;
            Category = NodeDefinitionHelper.GetCategory(template);
            DisplayName = NodeDefinitionHelper.GetDisplayName(template);
            TypeId = NodeDefinitionHelper.GetTypeId(template);
            IconSource = NodeDefinitionHelper.GetIconSource(template);
        }

        public NodeViewModel CreateNodeViewModel()
        {
            var clonedDefinition = NodeDefinitionHelper.Clone(Template);
            return new NodeViewModel(clonedDefinition);
        }
    }
}