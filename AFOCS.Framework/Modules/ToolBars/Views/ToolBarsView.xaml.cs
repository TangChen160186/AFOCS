using System.Windows.Controls;

namespace AFOCS.Framework.Modules.ToolBars.Views
{
    /// <summary>
    /// Interaction logic for ToolBarsView.xaml
    /// </summary>
    public partial class ToolBarsView : UserControl, IToolBarsView
    {
        ToolBarTray IToolBarsView.ToolBarTray
        {
            get { return ToolBarTray; }
        }

        public ToolBarsView()
        {
            InitializeComponent();
        }
    }
}
