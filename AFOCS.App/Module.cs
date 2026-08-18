using System.ComponentModel.Composition;
using AFOCS.App.Commands;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Menus;
using AFOCS.Framework.Modules.MainMenu;

namespace AFOCS.App
{
    [Export(typeof(IModule))]
    public class Module : ModuleBase
    {
        // ===== 菜单 =====

        [Export]
        public static readonly MenuDefinition TestMenu = new MenuDefinition(
            MenuDefinitions.MainMenuBar, 5, "测试");
         
        [Export]
        public static readonly MenuItemGroupDefinition TestMenuGroup = new MenuItemGroupDefinition(
            TestMenu, 0);

        // ===== "测试"菜单项 =====

        [Export]
        public static readonly MenuItemDefinition ViewTeachingPointTestMenuItem = new CommandMenuItemDefinition<ViewTeachingPointTestCommandDefinition>(
            TestMenuGroup, 0);

        [Export]
        public static readonly MenuItemDefinition ViewHomeTestMenuItem = new CommandMenuItemDefinition<ViewHomeTestCommandDefinition>(
            TestMenuGroup, 1);

        // ===== "视图"菜单项 =====

        [Export]
        public static readonly MenuItemDefinition ViewTestToolMenuItem = new CommandMenuItemDefinition<ViewTestToolCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 8);

        [Export]
        public static readonly MenuItemDefinition ViewTeachingPointsMenuItem = new CommandMenuItemDefinition<ViewTeachingPointsCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 9);

        [Export]
        public static readonly MenuItemDefinition ViewGamepadControlMenuItem = new CommandMenuItemDefinition<ViewGamepadControlCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 10);

        [Export]
        public static readonly MenuItemDefinition ViewFaPdCalibrationMenuItem = new CommandMenuItemDefinition<ViewFaPdCalibrationCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 11);

        // ===== 工位总览窗口 =====

        [Export]
        public static readonly MenuItemDefinition ViewLeftStationOverviewMenuItem = new CommandMenuItemDefinition<ViewLeftStationOverviewCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 12);

        [Export]
        public static readonly MenuItemDefinition ViewRightStationOverviewMenuItem = new CommandMenuItemDefinition<ViewRightStationOverviewCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 13);
    }
}
