using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;

namespace AFOCS.FlowNodeEditor.ViewModels
{
    /// <summary>
    /// 节点 ViewModel —— 每个节点拥有独立实例，属性值存在 C# 属性上。
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
        public ObservableCollection<PropertyItemViewModel> PropertyItems { get; } = [];

        public NodeViewModel(INodeDefinition instance, Guid? instanceId = null)
        {
            Definition = instance;
            InstanceId = instanceId ?? Guid.NewGuid();
            Title = instance.DisplayName;

            var defType = instance.GetType();

            // 端口：类级 + 属性级 Attribute 合并发现
            foreach (var port in NodeDefinitionScanner.ScanInputPorts(defType))
                Inputs.Add(new ConnectorViewModel(this, port, true));

            foreach (var port in NodeDefinitionScanner.ScanOutputPorts(defType))
                Outputs.Add(new ConnectorViewModel(this, port, false));

            // 属性：扫描 [NodeProperty]，ValueType 自动推断，DefaultValue 从实例读取
            foreach (var propDef in NodeDefinitionScanner.ScanProperties(defType, instance))
            {
                var propInfo = defType.GetProperty(propDef.Name);
                if (propInfo != null)
                {
                    PropertyItems.Add(new PropertyItemViewModel(instance, propInfo, propDef));
                    continue;
                }

                var fieldInfo = defType.GetField(propDef.Name);
                if (fieldInfo != null)
                {
                    PropertyItems.Add(new PropertyItemViewModel(instance, fieldInfo, propDef));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
