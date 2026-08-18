using System.Windows;
using System.Windows.Controls;
using AFOCS.App.ViewModels;
using HalconDotNet;

namespace AFOCS.App.Views;

/// <summary>相机监控视图基类：View 加载时将 HSmartWindowControlWPF 挂接到 ViewModel</summary>
public abstract class CameraToolViewBase : UserControl
{
    protected CameraToolViewBase()
    {
        Loaded += OnViewLoaded;
        Unloaded += OnViewUnloaded;
    }

    private void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CameraToolViewModelBase vm
            && FindName("HWindowControl") is HSmartWindowControlWPF control)
        {
            vm.SetHalconControl(control);
            EnsureContextMenu(vm, control);
        }
    }

    /// <summary>为相机窗口添加右键菜单（保存为 PNG）</summary>
    private static void EnsureContextMenu(CameraToolViewModelBase vm, HSmartWindowControlWPF control)
    {
        if (control.ContextMenu != null)
            return;

        var saveItem = new MenuItem { Header = "保存为 PNG" };
        saveItem.Click += (_, _) => vm.SaveAsPng();
        control.ContextMenu = new ContextMenu { Items = { saveItem } };
    }

    private void OnViewUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CameraToolViewModelBase vm)
            vm.ClearHalconControl();
    }
}
