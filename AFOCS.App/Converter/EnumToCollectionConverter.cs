using System.Globalization;
using System.Windows.Data;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;

namespace AFOCS.App.Converter;

public class EnumToCollectionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 优先从 parameter 获取枚举类型
        Type enumType = parameter as Type;

        // 如果 parameter 没有提供，尝试从 value 推断（兼容旧的绑定方式）
        if (enumType == null && value != null)
            enumType = value.GetType();

        if (enumType == null || !enumType.IsEnum)
            return Binding.DoNothing;

        return Enum.GetValues(enumType)
            .Cast<Enum>()
            // 工位选择只保留左/右工位，排除"通用"(None)
            .Where(e => enumType != typeof(WorkPos) || (WorkPos)e != WorkPos.None)
            .Select(e => new
            {
                Value = e,
                Description = e.GetDescription()
            })
            .ToList();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
