using AFOCS.App.ViewModels;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Results;
using Caliburn.Micro;

namespace AFOCS.App.Commands
{
    [CommandDefinition]
    public class ViewJogRightCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.JogRight";

        public override string Name => CommandName;

        public override string Text => "右工位手柄";

        public override string ToolTip => "打开右工位轴调试手柄";
    }

    [CommandHandler]
    public class ViewJogRightCommandHandler : CommandHandlerBase<ViewJogRightCommandDefinition>
    {
        public override async Task Run(Command command)
        {
            await Show.Tool<IJogRight>().ExecuteAsync();
        }
    }
}
