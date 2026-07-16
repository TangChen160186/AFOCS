using System.ComponentModel.Composition;
using AFOCS.Framework.Framework.Menus;
using AFOCS.Framework.Modules.UndoRedo.Commands;

namespace AFOCS.Framework.Modules.UndoRedo
{
    public static class MenuDefinitions
    {
        [Export]
        public static readonly MenuItemDefinition EditUndoMenuItem = new CommandMenuItemDefinition<UndoCommandDefinition>(
            MainMenu.MenuDefinitions.EditUndoRedoMenuGroup, 0);

        [Export]
        public static readonly MenuItemDefinition EditRedoMenuItem = new CommandMenuItemDefinition<RedoCommandDefinition>(
            MainMenu.MenuDefinitions.EditUndoRedoMenuGroup, 1);

        [Export]
        public static readonly MenuItemDefinition ViewHistoryMenuItem = new CommandMenuItemDefinition<ViewHistoryCommandDefinition>(
            MainMenu.MenuDefinitions.ViewToolsMenuGroup, 5);
    }
}
