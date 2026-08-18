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

        [Export]
        public static readonly MenuItemDefinition ViewRxCouplingCurveLeftMenuItem = new CommandMenuItemDefinition<ViewRxCouplingCurveLeftCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 12);

        [Export]
        public static readonly MenuItemDefinition ViewRxCouplingCurveRightMenuItem = new CommandMenuItemDefinition<ViewRxCouplingCurveRightCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 13);

        [Export]
        public static readonly MenuItemDefinition ViewTxCouplingCurveLeftMenuItem = new CommandMenuItemDefinition<ViewTxCouplingCurveLeftCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 14);

        [Export]
        public static readonly MenuItemDefinition ViewTxCouplingCurveRightMenuItem = new CommandMenuItemDefinition<ViewTxCouplingCurveRightCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 15);

        // ===== 相机实时监控 =====

        [Export]
        public static readonly MenuItemDefinition ViewLeftUpCameraMenuItem = new CommandMenuItemDefinition<ViewLeftUpCameraCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 16);

        [Export]
        public static readonly MenuItemDefinition ViewLeftDownCameraMenuItem = new CommandMenuItemDefinition<ViewLeftDownCameraCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 17);

        [Export]
        public static readonly MenuItemDefinition ViewRightUpCameraMenuItem = new CommandMenuItemDefinition<ViewRightUpCameraCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 18);

        [Export]
        public static readonly MenuItemDefinition ViewRightDownCameraMenuItem = new CommandMenuItemDefinition<ViewRightDownCameraCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 19);

        // ===== 工位 RSP/MPD 监控 =====

        [Export]
        public static readonly MenuItemDefinition ViewLeftStationMonitorMenuItem = new CommandMenuItemDefinition<ViewLeftStationMonitorCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 20);

        [Export]
        public static readonly MenuItemDefinition ViewRightStationMonitorMenuItem = new CommandMenuItemDefinition<ViewRightStationMonitorCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 21);
    }
}
