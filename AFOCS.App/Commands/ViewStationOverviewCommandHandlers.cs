using AFOCS.App.ViewModels;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Results;
using Caliburn.Micro;

namespace AFOCS.App.Commands;

[CommandHandler]
public class ViewLeftStationOverviewCommandHandler : CommandHandlerBase<ViewLeftStationOverviewCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await Show.Window<LeftStationOverviewViewModel>().ExecuteAsync();
    }
}

[CommandHandler]
public class ViewRightStationOverviewCommandHandler : CommandHandlerBase<ViewRightStationOverviewCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await Show.Window<RightStationOverviewViewModel>().ExecuteAsync();
    }
}