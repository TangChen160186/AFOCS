using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.Services
{
    public interface INodeRegistry
    {
        IReadOnlyList<INodeDefinition> AllDefinitions { get; }
        INodeDefinition? GetDefinition(string typeId);
        INodeDefinition? CreateInstance(string typeId);
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
            _lookup = _definitions.ToDictionary(d => NodeDefinitionHelper.GetTypeId(d));
        }

        public IReadOnlyList<INodeDefinition> AllDefinitions => _definitions;

        public INodeDefinition? GetDefinition(string typeId) =>
            _lookup.TryGetValue(typeId, out var def) ? def : null;

        public INodeDefinition? CreateInstance(string typeId)
        {
            var template = GetDefinition(typeId);
            return template != null ? NodeDefinitionHelper.Clone(template) : null;
        }

        public IReadOnlyList<IGrouping<string, INodeDefinition>> DefinitionsByCategory =>
            _definitions.GroupBy(d => NodeDefinitionHelper.GetCategory(d)).ToList();
    }
}