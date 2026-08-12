using System.Windows;
using AFOCS.VisionEditor.ViewModels;
using HalconDotNet;

namespace AFOCS.VisionEditor.Views;

public partial class VerifyDialog : Window
{
    public VerifyDialog(VerifyDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Owner = Application.Current.MainWindow;
    }

    private void HSmartVerify_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is VerifyDialogViewModel vm)
            vm.SetHalconControl(hSmartVerify);
    }
}
