using System.ComponentModel.Composition;
using AFOCS.App.Commands;
using AFOCS.App.ViewModels;
using AFOCS.Devices;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Menus;
using AFOCS.Infrastructure;
using Caliburn.Micro;

namespace AFOCS.App
{
    [Export(typeof(IModule))]
    public class Module : ModuleBase
    {
        [Export]
        public static readonly MenuItemDefinition ViewTestToolMenuItem = new CommandMenuItemDefinition<ViewTestToolCommandDefinition>(
            AFOCS.Framework.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 8);

        [Export]
        public static readonly MenuItemDefinition ViewTeachingPointsMenuItem = new CommandMenuItemDefinition<ViewTeachingPointsCommandDefinition>(
            AFOCS.Framework.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 9);



        public override IEnumerable<IDocument> DefaultDocuments
        {
            get
            {
                yield return new TeachingPointsDocumentViewModel(
                    IoC.Get<IConfigService>(),
                    IoC.Get<IBusAxisDevice>(),
                    IoC.GetAll<IAkribisMotion>());
            }
        }
    }
}
