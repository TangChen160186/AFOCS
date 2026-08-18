using AFOCS.App.ViewModels;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Results;
using Caliburn.Micro;

namespace AFOCS.App.Commands;

[CommandHandler]
public class ViewLeftStationMonitorCommandHandler : CommandHandlerBase<ViewLeftStationMonitorCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await Show.Tool<ILeftStationMonitorTool>().ExecuteAsync();
    }
}

[CommandHandler]
public class ViewRightStationMonitorCommandHandler : CommandHandlerBase<ViewRightStationMonitorCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await Show.Tool<IRightStationMonitorTool>().ExecuteAsync();
    }
}
