using System.Windows.Controls;

namespace AFOCS.App.Views
{
    public partial class GripperSettingsView : UserControl
    {
        public GripperSettingsView()
        {
            InitializeComponent();
        }
    }

    public class GripperLeftCouplingLSettingsView : GripperSettingsView;
    public class GripperLeftCouplingRSettingsView : GripperSettingsView;
    public class GripperRightCouplingLSettingsView : GripperSettingsView;
    public class GripperRightCouplingRSettingsView : GripperSettingsView;
}
