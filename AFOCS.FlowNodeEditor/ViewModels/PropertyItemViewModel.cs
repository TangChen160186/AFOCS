using System.ComponentModel;
using System.Runtime.CompilerServices;
using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.ViewModels
{
    /// <summary>
    /// 单个属性项的 ViewModel，支持属性面板中的双向绑定编辑
    /// </summary>
    public class PropertyItemViewModel : INotifyPropertyChanged
    {
        private readonly NodeViewModel _owner;

        public string Name { get; }
        public string DisplayName { get; }
        public NodePropertyValueType ValueType { get; }

        public object? Value
        {
            get => _owner.PropertyValues.TryGetValue(Name, out var val) ? val : null;
            set
            {
                if (_owner.PropertyValues.TryGetValue(Name, out var current) && Equals(current, value))
                    return;
                _owner.PropertyValues[Name] = value;
                Notify();
            }
        }

        public PropertyItemViewModel(NodeViewModel owner, INodePropertyDefinition propDef)
        {
            _owner = owner;
            Name = propDef.Name;
            DisplayName = propDef.DisplayName;
            ValueType = propDef.ValueType;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
