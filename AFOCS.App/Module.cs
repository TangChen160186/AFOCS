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
        // ===== 排除框架菜单中不显示的项目 =====

        [Export]
        public static readonly ExcludeMenuItemDefinition ExcludeToolboxMenuItem = new ExcludeMenuItemDefinition(
            AFOCS.Framework.Modules.Toolbox.MenuDefinitions.ViewToolboxMenuItem);

        [Export]
        public static readonly ExcludeMenuItemDefinition ExcludeHistoryMenuItem = new ExcludeMenuItemDefinition(
            AFOCS.Framework.Modules.UndoRedo.MenuDefinitions.ViewHistoryMenuItem);

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
        public static readonly MenuItemDefinition ViewCameraViewerMenuItem = new CommandMenuItemDefinition<CameraViewerCommandDefinition>(
            MenuDefinitions.ViewToolsMenuGroup, 12);

        // ===== 工位总览窗口（Window 菜单下） =====

        [Export]
        public static readonly MenuItemGroupDefinition WindowOverviewMenuGroup = new MenuItemGroupDefinition(
            MenuDefinitions.WindowMenu, 0);

        [Export]
        public static readonly MenuItemDefinition ViewLeftStationOverviewMenuItem = new CommandMenuItemDefinition<ViewLeftStationOverviewCommandDefinition>(
            WindowOverviewMenuGroup, 0);

        [Export]
        public static readonly MenuItemDefinition ViewRightStationOverviewMenuItem = new CommandMenuItemDefinition<ViewRightStationOverviewCommandDefinition>(
            WindowOverviewMenuGroup, 1);
    }
}
