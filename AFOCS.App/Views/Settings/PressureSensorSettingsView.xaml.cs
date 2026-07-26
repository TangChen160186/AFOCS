using System.Windows.Controls;

namespace AFOCS.App.Views.Settings
{
    public partial class PressureSensorSettingsView : UserControl
    {
        public PressureSensorSettingsView()
        {
            InitializeComponent();
        }
    }

    public class PressureSensorLeftCouplingLSettingsView : PressureSensorSettingsView;
    public class PressureSensorLeftCouplingRSettingsView : PressureSensorSettingsView;
    public class PressureSensorLeftDispenseSettingsView : PressureSensorSettingsView;
    public class PressureSensorRightCouplingLSettingsView : PressureSensorSettingsView;
    public class PressureSensorRightCouplingRSettingsView : PressureSensorSettingsView;
    public class PressureSensorRightDispenseSettingsView : PressureSensorSettingsView;
}
