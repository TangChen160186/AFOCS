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
        private bool _isExecuting;
        private bool _isCompleted;
        private bool _hasError;

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

        /// <summary>节点正在执行</summary>
        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                if (_isExecuting == value) return;
                _isExecuting = value;
                Notify();
            }
        }

        /// <summary>节点执行完成</summary>
        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted == value) return;
                _isCompleted = value;
                Notify();
            }
        }

        /// <summary>节点执行出错</summary>
        public bool HasError
        {
            get => _hasError;
            set
            {
                if (_hasError == value) return;
                _hasError = value;
                Notify();
            }
        }

        /// <summary>重置执行状态</summary>
        public void ResetExecutionState()
        {
            IsExecuting = false;
            IsCompleted = false;
            HasError = false;
        }

        public List<ConnectorViewModel> Inputs { get; } = [];
        public List<ConnectorViewModel> Outputs { get; } = [];

        public IEnumerable<ConnectorViewModel> ExecutionInputs => 
            Inputs.Where(c => c.PortType == NodePortType.Execution);
        public IEnumerable<ConnectorViewModel> DataInputs => 
            Inputs.Where(c => c.PortType != NodePortType.Execution);
        public IEnumerable<ConnectorViewModel> ExecutionOutputs => 
            Outputs.Where(c => c.PortType == NodePortType.Execution);
        public IEnumerable<ConnectorViewModel> DataOutputs => 
            Outputs.Where(c => c.PortType != NodePortType.Execution);

        public string Description => GetDescription();

        private string GetDescription()
        {
            return Definition.GetType()
                .GetProperty("Description")?.GetValue(Definition) as string ?? string.Empty;
        }

        public bool IsEnabled => GetIsEnabled();

        private bool GetIsEnabled()
        {
            return Definition.GetType()
                .GetProperty("Enabled")?.GetValue(Definition) as bool? ?? true;
        }

        public NodeViewModel(INodeDefinition definition, Guid? instanceId = null)
        {
            Definition = definition;
            InstanceId = instanceId ?? Guid.NewGuid();
            Title = NodeDefinitionHelper.GetDisplayName(definition);

            foreach (var port in NodeDefinitionHelper.GetInputPorts(definition))
                Inputs.Add(new ConnectorViewModel(this, port, true));

            foreach (var port in NodeDefinitionHelper.GetOutputPorts(definition))
                Outputs.Add(new ConnectorViewModel(this, port, false));

            if (definition is INotifyPropertyChanged notifyDef)
            {
                notifyDef.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == "Description")
                    {
                        Notify(nameof(Description));
                    }
                    else if (e.PropertyName == "Enabled")
                    {
                        Notify(nameof(IsEnabled));
                    }
                };
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}