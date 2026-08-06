using System.Windows;
using AFOCS.VisionEditor.ViewModels;

namespace AFOCS.VisionEditor.Views;

public partial class VerifyDialog : Window
{
    public VerifyDialog(VerifyDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Owner = Application.Current.MainWindow;
    }
}
