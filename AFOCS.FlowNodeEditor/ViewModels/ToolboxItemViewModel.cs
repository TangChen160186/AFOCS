using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.ViewModels
{
    /// <summary>
    /// 工具箱条目 ViewModel
    /// </summary>
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
            Category = definition.Category;
            DisplayName = definition.DisplayName;
            TypeId = definition.TypeId;
            IconSource = definition.IconSource;
        }

        public NodeViewModel CreateNodeViewModel() => new(Definition);
    }
}
