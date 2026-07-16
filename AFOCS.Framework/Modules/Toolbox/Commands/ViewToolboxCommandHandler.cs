using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Results;
using Caliburn.Micro;

namespace AFOCS.Framework.Modules.Toolbox.Commands
{
    [CommandHandler]
    public class ViewToolboxCommandHandler : CommandHandlerBase<ViewToolboxCommandDefinition>
    {
        public override async Task Run(Command command)
        {
            await Show.Tool<IToolbox>().ExecuteAsync();
        }
    }
}
