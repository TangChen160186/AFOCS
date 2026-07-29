using AFOCS.App.ViewModels;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Results;
using Caliburn.Micro;

namespace AFOCS.App.Commands;

[CommandHandler]
public class ViewTeachingPointTestCommandHandler : CommandHandlerBase<ViewTeachingPointTestCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await Show.Tool<ITeachingPointTest>().ExecuteAsync();
    }
}
