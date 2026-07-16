using AFOCS.Framework.Framework.Menus;
using AFOCS.Framework.Modules.MainMenu.Models;

namespace AFOCS.Framework.Modules.MainMenu
{
    public interface IMenuBuilder
    {
        void BuildMenuBar(MenuBarDefinition menuBarDefinition, MenuModel result);
    }
}