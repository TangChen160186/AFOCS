using System.ComponentModel.Composition;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Menus;

namespace AFOCS.FlowNodeEditor
{
    [Export(typeof(IModule))]
    public class Module : ModuleBase
    {
        [Export]
        public static readonly MenuItemGroupDefinition NodeEditorMenuGroup = new MenuItemGroupDefinition(
            AFOCS.Framework.Modules.MainMenu.MenuDefinitions.ViewMenu, 5);

        public override IEnumerable<IDocument> DefaultDocuments
        {
            get
            {
                // 启动时自动打开一个空的节点编辑器
                yield return new ViewModels.NodeEditorDocumentViewModel(
                    AppBootstrapper.GetInstance<Services.INodeRegistry>());
            }
        }
    }
}
