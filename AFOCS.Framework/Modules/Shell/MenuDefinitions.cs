using System.ComponentModel.Composition;
using AFOCS.Framework.Framework.Menus;
using AFOCS.Framework.Modules.Shell.Commands;
using AFOCS.Framework.Properties;

namespace AFOCS.Framework.Modules.Shell
{
    public static class MenuDefinitions
    {
        [Export]
        public static readonly MenuItemDefinition FileNewMenuItem = new TextMenuItemDefinition(
            MainMenu.MenuDefinitions.FileNewOpenMenuGroup, 0, Resources.FileNewCommandText);

        [Export]
        public static readonly MenuItemGroupDefinition FileNewCascadeGroup = new MenuItemGroupDefinition(
            FileNewMenuItem, 0);

        [Export]
        public static readonly MenuItemDefinition FileNewMenuItemList = new CommandMenuItemDefinition<NewFileCommandListDefinition>(
            FileNewCascadeGroup, 0);

        [Export]
        public static readonly MenuItemDefinition FileOpenMenuItem = new CommandMenuItemDefinition<OpenFileCommandDefinition>(
            MainMenu.MenuDefinitions.FileNewOpenMenuGroup, 1);

        [Export]
        public static readonly MenuItemDefinition FileCloseMenuItem = new CommandMenuItemDefinition<CloseFileCommandDefinition>(
            MainMenu.MenuDefinitions.FileCloseMenuGroup, 0);

        [Export]
        public static readonly MenuItemDefinition FileSaveMenuItem = new CommandMenuItemDefinition<SaveFileCommandDefinition>(
            MainMenu.MenuDefinitions.FileSaveMenuGroup, 0);

        [Export]
        public static readonly MenuItemDefinition FileSaveAsMenuItem = new CommandMenuItemDefinition<SaveFileAsCommandDefinition>(
            MainMenu.MenuDefinitions.FileSaveMenuGroup, 1);

        [Export]
        public static readonly MenuItemDefinition FileSaveAllMenuItem = new CommandMenuItemDefinition<SaveAllFilesCommandDefinition>(
            MainMenu.MenuDefinitions.FileSaveMenuGroup, 1);

        [Export]
        public static readonly MenuItemDefinition FileExitMenuItem = new CommandMenuItemDefinition<ExitCommandDefinition>(
            MainMenu.MenuDefinitions.FileExitOpenMenuGroup, 0);

        [Export]
        public static readonly MenuItemDefinition WindowDocumentList = new CommandMenuItemDefinition<SwitchToDocumentCommandListDefinition>(
            MainMenu.MenuDefinitions.WindowDocumentListMenuGroup, 0);

        [Export]
        public static readonly MenuItemDefinition ViewFullscreenItem = new CommandMenuItemDefinition<ViewFullScreenCommandDefinition>(
            MainMenu.MenuDefinitions.ViewPropertiesMenuGroup, 0);
    }
}
