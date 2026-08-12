using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AFOCS.VisionEditor.Models;
using HalconDotNet;

namespace AFOCS.VisionEditor.Views;

public partial class VisionEditorDocumentView : UserControl
{
    public VisionEditorDocumentView()
    {
        InitializeComponent();
    }

    private void HSmart_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.VisionEditorDocumentViewModel vm)
        {
            vm.SetHalconControl(hSmart);
        }
    }
}

// ===== Converters =====

public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}

public class InvertBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NccToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is VisionProcessType.Ncc ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
