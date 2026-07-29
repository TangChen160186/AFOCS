using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace AFOCS.App.Converter;

public class DescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string propName && parameter is Type type)
        {
            var prop = type.GetProperty(propName);
            var attr = prop?.GetCustomAttribute<DescriptionAttribute>();
            return attr?.Description ?? propName;
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}