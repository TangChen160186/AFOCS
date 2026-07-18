using System.ComponentModel.Composition;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Services;
using AFOCS.Framework.Framework.Threading;

namespace AFOCS.Framework.Inspector.Commands
{
    [CommandHandler]
    public class ViewInspectorCommandHandler : CommandHandlerBase<ViewInspectorCommandDefinition>
    {
        private readonly IShell _shell;

        [ImportingConstructor]
        public ViewInspectorCommandHandler(IShell shell)
        {
            _shell = shell;
        }

        public override Task Run(Command command)
        {
            _shell.ShowTool<IInspectorTool>();
            return TaskUtility.Completed;
        }
    }
}