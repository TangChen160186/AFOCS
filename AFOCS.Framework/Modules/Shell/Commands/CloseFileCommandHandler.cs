using System.ComponentModel.Composition;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Services;

namespace AFOCS.Framework.Modules.Shell.Commands
{
    [CommandHandler]
    public class CloseFileCommandHandler : CommandHandlerBase<CloseFileCommandDefinition>
    {
        private readonly IShell _shell;

        [ImportingConstructor]
        public CloseFileCommandHandler(IShell shell)
        {
            _shell = shell;
        }

        public override void Update(Command command)
        {
            command.Enabled = _shell.ActiveItem != null;
            base.Update(command);
        }

        public override Task Run(Command command)
        {
            return _shell.CloseDocumentAsync(_shell.ActiveItem);
        }
    }
}
