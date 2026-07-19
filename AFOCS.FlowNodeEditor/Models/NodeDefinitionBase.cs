using System.ComponentModel;
using System.Runtime.CompilerServices;
using Caliburn.Micro;

namespace AFOCS.FlowNodeEditor.Models
{
    public class NodeDefinitionBase : PropertyChangedBase, INodeDefinition
    {
        private string _description = string.Empty;
        [DisplayName("描述")]
        public string Description
        {
            get => _description;
            set => Set(ref _description, value);
        }

        private bool _enabled = true;
        [DisplayName("启用")]
        public bool Enabled
        {
            get => _enabled;
            set => Set(ref _enabled, value);
        }

    }
}
