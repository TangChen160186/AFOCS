using System.ComponentModel.Composition;
using AFOCS.Framework.Framework.ToolBars;
using AFOCS.Framework.Properties;

namespace AFOCS.Framework.Modules.ToolBars
{
    internal static class ToolBarDefinitions
    {
        [Export]
        public static ToolBarDefinition StandardToolBar = new ToolBarDefinition(0, Resources.ToolBarStandard);
    }
}