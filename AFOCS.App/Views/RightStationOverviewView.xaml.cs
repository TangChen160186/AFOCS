using System.Windows;

namespace AFOCS.App.Views;

public partial class RightStationOverviewView : Window
{
    public RightStationOverviewView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 总览窗口不应作为主窗口的子窗口：清除框架推断的 Owner 与置顶，使其可被主窗口覆盖
        Owner = null;
        Topmost = false;
    }
}