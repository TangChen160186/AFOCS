using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AFOCS.FlowNodeEditor.Models
{
    public class NodeDefinitionBase : INodeDefinition
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _description = string.Empty;
        [DisplayName("描述")]
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private bool _enabled = true;
        [DisplayName("启用")]
        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
