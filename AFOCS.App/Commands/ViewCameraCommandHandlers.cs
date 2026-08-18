using AFOCS.App.ViewModels;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Results;
using Caliburn.Micro;

namespace AFOCS.App.Commands;

[CommandHandler]
public class ViewLeftUpCameraCommandHandler : CommandHandlerBase<ViewLeftUpCameraCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await Show.Tool<ILeftUpCameraTool>().ExecuteAsync();
    }
}

[CommandHandler]
public class ViewLeftDownCameraCommandHandler : CommandHandlerBase<ViewLeftDownCameraCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await Show.Tool<ILeftDownCameraTool>().ExecuteAsync();
    }
}

[CommandHandler]
public class ViewRightUpCameraCommandHandler : CommandHandlerBase<ViewRightUpCameraCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await Show.Tool<IRightUpCameraTool>().ExecuteAsync();
    }
}

[CommandHandler]
public class ViewRightDownCameraCommandHandler : CommandHandlerBase<ViewRightDownCameraCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await Show.Tool<IRightDownCameraTool>().ExecuteAsync();
    }
}
