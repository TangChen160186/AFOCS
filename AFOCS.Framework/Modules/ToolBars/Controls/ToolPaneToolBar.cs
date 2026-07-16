using System.Windows;
using System.Windows.Controls;

namespace AFOCS.Framework.Modules.ToolBars.Controls
{
    public class ToolPaneToolBar : ToolBarBase
    {
        static ToolPaneToolBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ToolPaneToolBar),
                new FrameworkPropertyMetadata(typeof(ToolPaneToolBar)));
        }

        public ToolPaneToolBar()
        {
            SetOverflowMode(this, OverflowMode.AsNeeded);
            ToolBarTray.SetIsLocked(this, true);
        }
    }
}