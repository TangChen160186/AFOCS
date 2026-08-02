using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Views;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Menus;
using AFOCS.Framework.Modules.MainMenu;
using AFOCS.VisionEditor.Commands;
using AFOCS.VisionEditor.ViewModels;
using Caliburn.Micro;

namespace AFOCS.VisionEditor
{
    [Export(typeof(IModule))]
    public class Module : ModuleBase
    {
        static Module()
        {
            // 视觉编辑器复用流程节点编辑器的界面（WPF UserControl XAML 不支持类继承，
            // 因此通过 ViewLocator 将 VisionEditorDocumentViewModel 映射到 NodeEditorDocumentView）
            var defaultLocate = ViewLocator.LocateTypeForModelType;
            ViewLocator.LocateTypeForModelType = (modelType, displayName, context) =>
                modelType == typeof(VisionEditorDocumentViewModel)
                    ? typeof(NodeEditorDocumentView)
                    : defaultLocate(modelType, displayName, context);
        }

        // 在"视图"菜单的工具组末尾添加"视觉编辑器"入口
        [Export]
        public static readonly MenuItemDefinition ViewVisionEditorMenuItem =
            new CommandMenuItemDefinition<ViewVisionEditorCommandDefinition>(
                MenuDefinitions.ViewToolsMenuGroup, 11);
    }
}
