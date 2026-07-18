using System.Windows;
using System.Windows.Controls;
using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.Views
{
    /// <summary>
    /// 根据属性值类型选择对应的编辑器模板
    /// </summary>
    public class PropertyEditorSelector : DataTemplateSelector
    {
        public DataTemplate? StringTemplate { get; set; }
        public DataTemplate? IntTemplate { get; set; }
        public DataTemplate? DoubleTemplate { get; set; }
        public DataTemplate? BoolTemplate { get; set; }
        public DataTemplate? EnumTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is ViewModels.PropertyItemViewModel prop)
            {
                return prop.ValueType switch
                {
                    NodePropertyValueType.Int => IntTemplate ?? StringTemplate,
                    NodePropertyValueType.Double => DoubleTemplate ?? StringTemplate,
                    NodePropertyValueType.Bool => BoolTemplate ?? StringTemplate,
                    NodePropertyValueType.Enum => EnumTemplate ?? StringTemplate,
                    _ => StringTemplate,
                };
            }
            return base.SelectTemplate(item, container);
        }
    }
}
