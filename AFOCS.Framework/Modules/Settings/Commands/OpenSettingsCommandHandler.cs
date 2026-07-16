using System.ComponentModel.Composition;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Modules.Settings.ViewModels;
using Caliburn.Micro;

namespace AFOCS.Framework.Modules.Settings.Commands
{
    [CommandHandler]
    public class OpenSettingsCommandHandler : CommandHandlerBase<OpenSettingsCommandDefinition>
    {
        private readonly IWindowManager _windowManager;

        [ImportingConstructor]
        public OpenSettingsCommandHandler(IWindowManager windowManager)
        {
            _windowManager = windowManager;
        }

        public override async Task Run(Command command)
        {
            await _windowManager.ShowDialogAsync(IoC.Get<SettingsViewModel>());
        }
    }
}
