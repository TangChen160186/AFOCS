using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.ViewModels
{
    /// <summary>
    /// 节点 ViewModel —— NodifyEditor.ItemsSource 中的每一项都对应一个 NodeViewModel
    /// </summary>
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

        /// <summary>节点属性值（属性名 -> 实际值）</summary>
        public Dictionary<string, object?> PropertyValues { get; } = [];

        /// <summary>属性面板可编辑属性列表</summary>
        public ObservableCollection<PropertyItemViewModel> PropertyItems { get; } = [];

        public NodeViewModel(INodeDefinition definition, Guid? instanceId = null)
        {
            Definition = definition;
            InstanceId = instanceId ?? Guid.NewGuid();
            Title = definition.DisplayName;

            foreach (var port in definition.InputPorts)
                Inputs.Add(new ConnectorViewModel(this, port, true));

            foreach (var port in definition.OutputPorts)
                Outputs.Add(new ConnectorViewModel(this, port, false));

            foreach (var prop in definition.Properties)
            {
                PropertyValues[prop.Name] = prop.DefaultValue;
                PropertyItems.Add(new PropertyItemViewModel(this, prop));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
