using System.Windows;
using System.Windows.Controls;
using AFOCS.App.ViewModels;

namespace AFOCS.App.Views;

public partial class RxCouplingCurveRightView : UserControl
{
    private RxCouplingCurveViewModelBase? _vm;

    public RxCouplingCurveRightView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is RxCouplingCurveViewModelBase vm)
        {
            _vm = vm;
            PlotControl.Reset(vm.Plot);
            vm.PlotUpdated += OnPlotUpdated;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm != null)
            _vm.PlotUpdated -= OnPlotUpdated;
        _vm = null;
    }

    private void OnPlotUpdated() => PlotControl.Refresh();
}
