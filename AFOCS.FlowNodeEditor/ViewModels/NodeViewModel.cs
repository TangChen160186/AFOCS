using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.ViewModels
{
    public class NodeViewModel : INotifyPropertyChanged
    {
        private Point _location;
        private string _title = string.Empty;
        private bool _isSelected;

        public Guid InstanceId { get; }
        public INodeDefinition Definition { get; }

        public string Title
        {
            get => _title;
            set { _title = value; Notify(); }
        }

        public Point Location
        {
            get => _location;
            set { _location = value; Notify(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                Notify();
            }
        }

        public List<ConnectorViewModel> Inputs { get; } = [];
        public List<ConnectorViewModel> Outputs { get; } = [];

        public NodeViewModel(INodeDefinition definition, Guid? instanceId = null)
        {
            Definition = definition;
            InstanceId = instanceId ?? Guid.NewGuid();
            Title = NodeDefinitionHelper.GetDisplayName(definition);

            foreach (var port in NodeDefinitionHelper.GetInputPorts(definition))
                Inputs.Add(new ConnectorViewModel(this, port, true));

            foreach (var port in NodeDefinitionHelper.GetOutputPorts(definition))
                Outputs.Add(new ConnectorViewModel(this, port, false));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}