using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Menus;
using AFOCS.Framework.Test.Modules.Test.Commands;
using AFOCS.Framework.Test.Modules.Test.ViewModels;
using Caliburn.Micro;
using System.ComponentModel.Composition;

namespace AFOCS.Framework.Test.Modules.Test
{
    [Export(typeof(IModule))]
    public class Module : ModuleBase
    {
        [Export]
        public static readonly MenuItemGroupDefinition ViewDemoMenuGroup = new MenuItemGroupDefinition(
            AFOCS.Framework.Modules.MainMenu.MenuDefinitions.ViewMenu, 10);


        [Export]
        public static readonly MenuItemDefinition ViewHomeMenuItem = new CommandMenuItemDefinition<ViewHomeCommandDefinition>(
            ViewDemoMenuGroup, 0);
    
    }
}
