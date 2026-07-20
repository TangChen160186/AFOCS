using System.ComponentModel.Composition;
using AFOCS.App.Commands;
using AFOCS.App.ViewModels;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Menus;

namespace AFOCS.App
{
    [Export(typeof(IModule))]
    public class Module : ModuleBase
    {
        [Export]
        public static readonly MenuItemDefinition ViewJogLeftMenuItem = new CommandMenuItemDefinition<ViewJogLeftCommandDefinition>(
            AFOCS.Framework.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 5);

        [Export]
        public static readonly MenuItemDefinition ViewJogRightMenuItem = new CommandMenuItemDefinition<ViewJogRightCommandDefinition>(
            AFOCS.Framework.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 6);

        public override IEnumerable<Type> DefaultTools
        {
            get
            {
                yield return typeof(IJogLeft);
                yield return typeof(IJogRight);
            }
        }
    }
}
