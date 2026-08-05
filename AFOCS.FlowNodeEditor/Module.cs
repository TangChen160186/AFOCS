using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Menus;
using System.ComponentModel.Composition;

namespace AFOCS.FlowNodeEditor;

[Export(typeof(IModule))]
public class Module : ModuleBase
{
    [Export]
    public static readonly MenuItemGroupDefinition NodeEditorMenuGroup = new MenuItemGroupDefinition(
        AFOCS.Framework.Modules.MainMenu.MenuDefinitions.ViewMenu, 5);

  
}