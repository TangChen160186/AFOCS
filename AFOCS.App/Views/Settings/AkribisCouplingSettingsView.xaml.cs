using System.Windows.Controls;

namespace AFOCS.App.Views.Settings;

public partial class AkribisCouplingSettingsView : UserControl
{
    public AkribisCouplingSettingsView()
    {
        InitializeComponent();
    }
}

public class AkribisLeftCouplingLSettingsView : AkribisCouplingSettingsView;
public class AkribisLeftCouplingRSettingsView : AkribisCouplingSettingsView;
public class AkribisRightCouplingLSettingsView : AkribisCouplingSettingsView;
public class AkribisRightCouplingRSettingsView : AkribisCouplingSettingsView;

