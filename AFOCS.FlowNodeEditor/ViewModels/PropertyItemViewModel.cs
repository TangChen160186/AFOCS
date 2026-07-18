using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.ViewModels
{
    /// <summary>
    /// 单个属性项的 ViewModel，通过反射直接读写节点实例上的 C# 属性/字段，
    /// 支持属性面板中的双向绑定编辑。
    /// </summary>
    public class PropertyItemViewModel : INotifyPropertyChanged
    {
        private readonly object _instance;
        private readonly Func<object?>? _getter;
        private readonly Action<object?>? _setter;

        public string Name { get; }
        public string DisplayName { get; }
        public NodePropertyValueType ValueType { get; }

        /// <summary>值通过反射直接读写节点实例上的属性/字段</summary>
        public object? Value
        {
            get => _getter?.Invoke();
            set
            {
                if (_setter == null) return;
                var current = _getter?.Invoke();
                if (Equals(current, value)) return;
                _setter(value);
                Notify();
            }
        }

        /// <summary>通过 PropertyInfo 创建（C# 属性）</summary>
        public PropertyItemViewModel(object instance, PropertyInfo prop, INodePropertyDefinition def)
        {
            _instance = instance;
            Name = def.Name;
            DisplayName = def.DisplayName;
            ValueType = def.ValueType;
            _getter = () => prop.GetValue(_instance);
            _setter = v => prop.SetValue(_instance, v);
        }

        /// <summary>通过 FieldInfo 创建（C# 字段）</summary>
        public PropertyItemViewModel(object instance, FieldInfo field, INodePropertyDefinition def)
        {
            _instance = instance;
            Name = def.Name;
            DisplayName = def.DisplayName;
            ValueType = def.ValueType;
            _getter = () => field.GetValue(_instance);
            _setter = v => field.SetValue(_instance, v);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
