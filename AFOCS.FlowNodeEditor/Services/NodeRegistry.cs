using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.Services
{
    /// <summary>
    /// 节点注册中心，通过 MEF 发现所有导出的 INodeDefinition
    /// </summary>
    public interface INodeRegistry
    {
        IReadOnlyList<INodeDefinition> AllDefinitions { get; }
        INodeDefinition? GetDefinition(string typeId);
        IReadOnlyList<IGrouping<string, INodeDefinition>> DefinitionsByCategory { get; }
    }

    [Export(typeof(INodeRegistry))]
    public class NodeRegistry : INodeRegistry
    {
        private readonly IReadOnlyList<INodeDefinition> _definitions;
        private readonly Dictionary<string, INodeDefinition> _lookup;

        [ImportingConstructor]
        public NodeRegistry([ImportMany] IEnumerable<INodeDefinition> definitions)
        {
            _definitions = definitions.ToList();
            _lookup = _definitions.ToDictionary(d => d.TypeId);
        }

        public IReadOnlyList<INodeDefinition> AllDefinitions => _definitions;

        public INodeDefinition? GetDefinition(string typeId) =>
            _lookup.TryGetValue(typeId, out var def) ? def : null;

        public IReadOnlyList<IGrouping<string, INodeDefinition>> DefinitionsByCategory =>
            _definitions.GroupBy(d => d.Category).ToList();
    }
}
