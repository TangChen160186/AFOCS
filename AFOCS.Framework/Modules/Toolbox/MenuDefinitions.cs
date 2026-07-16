using System.ComponentModel.Composition;
using AFOCS.Framework.Framework.Menus;
using AFOCS.Framework.Modules.Toolbox.Commands;

namespace AFOCS.Framework.Modules.Toolbox
{
    public static class MenuDefinitions
    {
        [Export]
        public static readonly MenuItemDefinition ViewToolboxMenuItem = new CommandMenuItemDefinition<ViewToolboxCommandDefinition>(
            MainMenu.MenuDefinitions.ViewToolsMenuGroup, 4);
    }
}
