using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.Services
{
    /// <summary>
    /// 节点注册中心，通过 MEF 发现所有导出的 INodeDefinition
    /// </summary>
    public interface INodeRegistry
    {
        /// <summary>所有已注册的节点定义</summary>
        IReadOnlyList<INodeDefinition> AllDefinitions { get; }

        /// <summary>获取节点定义元数据</summary>
        INodeDefinition? GetDefinition(string typeId);

        /// <summary>按分类分组的节点定义</summary>
        IReadOnlyList<IGrouping<string, INodeDefinition>> DefinitionsByCategory { get; }

        /// <summary>为指定 TypeId 创建新的节点实例（每个节点拥有独立状态）</summary>
        INodeDefinition CreateInstance(string typeId);
    }

    [Export(typeof(INodeRegistry))]
    public class NodeRegistry : INodeRegistry
    {
        private readonly IReadOnlyList<INodeDefinition> _definitions;
        private readonly Dictionary<string, INodeDefinition> _lookup;
        private readonly Dictionary<string, Type> _typeLookup;

        [ImportingConstructor]
        public NodeRegistry([ImportMany] IEnumerable<INodeDefinition> definitions)
        {
            _definitions = definitions.ToList();
            _lookup = _definitions.ToDictionary(d => d.TypeId);
            _typeLookup = _definitions.ToDictionary(d => d.TypeId, d => d.GetType());
        }

        public IReadOnlyList<INodeDefinition> AllDefinitions => _definitions;

        public INodeDefinition? GetDefinition(string typeId) =>
            _lookup.TryGetValue(typeId, out var def) ? def : null;

        public INodeDefinition CreateInstance(string typeId)
        {
            if (!_typeLookup.TryGetValue(typeId, out var type))
                throw new InvalidOperationException($"未找到节点类型: {typeId}");

            return (INodeDefinition)Activator.CreateInstance(type)!;
        }

        public IReadOnlyList<IGrouping<string, INodeDefinition>> DefinitionsByCategory =>
            _definitions.GroupBy(d => d.Category).ToList();
    }
}
