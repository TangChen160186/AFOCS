using AFOCS.App.ViewModels;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Results;
using Caliburn.Micro;

namespace AFOCS.App.Commands
{
    [CommandDefinition]
    public class ViewJogLeftCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.JogLeft";

        public override string Name => CommandName;

        public override string Text => "左工位手柄";

        public override string ToolTip => "打开左工位轴调试手柄";
    }

    [CommandHandler]
    public class ViewJogLeftCommandHandler : CommandHandlerBase<ViewJogLeftCommandDefinition>
    {
        public override async Task Run(Command command)
        {
            await Show.Tool<IJogLeft>().ExecuteAsync();
        }
    }
}
