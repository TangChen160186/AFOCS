using System.ComponentModel.Composition;
using AFOCS.Framework.Framework.Menus;
using AFOCS.Framework.Inspector.Commands;

namespace AFOCS.Framework.Inspector
{
    public static class MenuDefinitions
    {
        [Export]
        public static readonly MenuItemDefinition ViewInspectorMenuItem = new CommandMenuItemDefinition<ViewInspectorCommandDefinition>(
            AFOCS.Framework.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 1);
    }
}
