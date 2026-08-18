using AFOCS.App.ViewModels;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Results;
using Caliburn.Micro;

namespace AFOCS.App.Commands;

[CommandHandler]
public class ViewLeftFlowMonitorCommandHandler : CommandHandlerBase<ViewLeftFlowMonitorCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await Show.Tool<ILeftFlowMonitorTool>().ExecuteAsync();
    }
}

[CommandHandler]
public class ViewRightFlowMonitorCommandHandler : CommandHandlerBase<ViewRightFlowMonitorCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await Show.Tool<IRightFlowMonitorTool>().ExecuteAsync();
    }
}
