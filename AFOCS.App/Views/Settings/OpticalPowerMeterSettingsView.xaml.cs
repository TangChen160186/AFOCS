using System.Windows.Controls;

namespace AFOCS.App.Views.Settings
{
    public partial class OpticalPowerMeterSettingsView : UserControl
    {
        public OpticalPowerMeterSettingsView()
        {
            InitializeComponent();
        }
    }


    public class OpticalPowerMeterLeftSettingsView : OpticalPowerMeterSettingsView;
    public class OpticalPowerMeterRightSettingsView : OpticalPowerMeterSettingsView;
}
