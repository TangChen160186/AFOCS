using System.ComponentModel.Composition;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Results;
using AFOCS.Framework.Test.Modules.Test.ViewModels;
using Caliburn.Micro;

namespace AFOCS.Framework.Test.Modules.Test.Commands
{


    [CommandHandler]
    public class ViewHomeCommandHandler : CommandHandlerBase<ViewHomeCommandDefinition>
    {
        public override async Task Run(Command command)
        {
            await Show.Document<TestViewModel>().ExecuteAsync();
        }
    }
}
