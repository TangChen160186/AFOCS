using System.Windows;
using System.Windows.Controls;

namespace AFOCS.App.Views;

public partial class JogStationView : UserControl
{
    public JogStationView()
    {
        InitializeComponent();
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BusPanel is null || AkribisPanel is null) return;

        var tabControl = (TabControl)sender;
        BusPanel.Visibility = tabControl.SelectedIndex == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        AkribisPanel.Visibility = tabControl.SelectedIndex == 1
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
