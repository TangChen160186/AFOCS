using System.Windows.Controls;

namespace AFOCS.App.Views
{
    public partial class CameraSettingsView : UserControl
    {
        public CameraSettingsView()
        {
            InitializeComponent();
        }
    }

    public class CameraLeftUpSettingsView : CameraSettingsView;
    public class CameraLeftDownSettingsView : CameraSettingsView;
    public class CameraRightUpSettingsView : CameraSettingsView;
    public class CameraRightDownSettingsView : CameraSettingsView;
}
