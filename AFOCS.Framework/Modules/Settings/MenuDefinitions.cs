using System.ComponentModel.Composition;
using AFOCS.Framework.Framework.Menus;
using AFOCS.Framework.Modules.Settings.Commands;

namespace AFOCS.Framework.Modules.Settings
{
    public static class MenuDefinitions
    {
        [Export]
        public static readonly MenuItemDefinition OpenSettingsMenuItem = new CommandMenuItemDefinition<OpenSettingsCommandDefinition>(
            MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 0);
    }
}
