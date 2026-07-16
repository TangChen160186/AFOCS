using System.ComponentModel.Composition;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Services;
using AFOCS.Framework.Framework.Threading;

namespace AFOCS.Framework.Modules.Shell.Commands
{
    [CommandHandler]
    public class ExitCommandHandler : CommandHandlerBase<ExitCommandDefinition>
    {
        private readonly IShell _shell;

        [ImportingConstructor]
        public ExitCommandHandler(IShell shell)
        {
            _shell = shell;
        }

        public override Task Run(Command command)
        {
            _shell.Close();
            return TaskUtility.Completed;
        }
    }
}