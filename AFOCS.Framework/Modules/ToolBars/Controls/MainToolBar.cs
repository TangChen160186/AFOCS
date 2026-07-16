using System.Windows.Controls;

namespace AFOCS.Framework.Modules.ToolBars.Controls
{
    public class MainToolBar : ToolBarBase
    {
        public MainToolBar()
        {
            SetOverflowMode(this, OverflowMode.Always);
            SetResourceReference(StyleProperty, typeof(ToolBar));
        }
    }
}