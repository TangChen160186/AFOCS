using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AFOCS.FlowNodeEditor.Converters
{
    /// <summary>
    /// 反转布尔值到 Visibility 的转换器。非 true 值视为 false。
    /// </summary>
    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var isVisible = value is true;
            return isVisible ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility vis)
                return vis != Visibility.Visible;
            return true;
        }
    }
}
