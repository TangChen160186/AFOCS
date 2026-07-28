using AFOCS.App.ViewModels;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Results;
using Caliburn.Micro;

namespace AFOCS.App.Commands;

[CommandHandler]
public class ViewTestToolCommandHandler : CommandHandlerBase<ViewTestToolCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await Show.Tool<ITestTool>().ExecuteAsync();
    }
}
