using System.Windows;
using System.Windows.Controls;

namespace AFOCS.Framework.Modules.ToolBars.Controls
{
    public class ToolBarTrayContainer : ContentControl
    {
        static ToolBarTrayContainer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ToolBarTrayContainer),
                new FrameworkPropertyMetadata(typeof(ToolBarTrayContainer)));
        } 
    }
}