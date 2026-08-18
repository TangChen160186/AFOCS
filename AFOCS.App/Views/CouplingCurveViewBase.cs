using System.Windows;
using System.Windows.Controls;
using AFOCS.App.ViewModels;
using ScottPlot.WPF;

namespace AFOCS.App.Views;

/// <summary>耦合曲线视图基类：挂接 ViewModel 的 Plot 并订阅刷新</summary>
public abstract class CouplingCurveViewBase : UserControl
{
    private CouplingCurveViewModelBase? _vm;
    private WpfPlot? _plot;

    protected CouplingCurveViewBase()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CouplingCurveViewModelBase vm && FindName("PlotControl") is WpfPlot plot)
        {
            _vm = vm;
            _plot = plot;
            plot.Reset(vm.Plot);
            vm.PlotUpdated += OnPlotUpdated;

            // 延迟到布局完成后刷新，确保轴标签首次加载即显示、无需交互
            Dispatcher.BeginInvoke(new System.Action(() => plot.Refresh()), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm != null)
            _vm.PlotUpdated -= OnPlotUpdated;
        _vm = null;
        _plot = null;
    }

    private void OnPlotUpdated() => _plot?.Refresh();
}
