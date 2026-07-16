using System.ComponentModel.Composition;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Services;

namespace AFOCS.Framework.Modules.Shell.Commands
{
    [CommandHandler]
    public class SaveAllFilesCommandHandler : CommandHandlerBase<SaveAllFilesCommandDefinition>
    {
        private readonly IShell _shell;

        [ImportingConstructor]
        public SaveAllFilesCommandHandler(IShell shell)
        {
            _shell = shell;
        }

        public override async Task Run(Command command)
        {
            var tasks = new List<Task<Tuple<IPersistedDocument, bool>>>();

            foreach (var document in _shell.Documents.OfType<IPersistedDocument>().Where(x => !x.IsNew))
            {
                await document.Save(document.FilePath);
            }

            // TODO: display "Item(s) saved" in statusbar
        }
    }
}
