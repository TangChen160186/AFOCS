using AFOCS.App.ViewModels;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Results;
using Caliburn.Micro;

namespace AFOCS.App.Commands;

[CommandHandler]
public class ViewGamepadControlCommandHandler : CommandHandlerBase<ViewGamepadControlCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await Show.Tool<IGamepadControl>().ExecuteAsync();
    }
}
