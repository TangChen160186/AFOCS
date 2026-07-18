using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;

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

        /// <summary>创建节点 ViewModel，通过注册中心生成独立的节点实例</summary>
        public NodeViewModel CreateNodeViewModel(INodeRegistry registry)
            => new(registry.CreateInstance(TypeId));
    }
}
