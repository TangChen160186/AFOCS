using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Results;
using AFOCS.VisionEditor.Services;
using AFOCS.VisionEditor.ViewModels;
using Caliburn.Micro;

namespace AFOCS.VisionEditor.Commands;

[CommandHandler]
public class ViewVisionEditorCommandHandler : CommandHandlerBase<ViewVisionEditorCommandDefinition>
{
    public override async Task Run(Command command)
    {
        var document = new VisionEditorDocumentViewModel(IoC.Get<IVisionNodeRegistry>());
        await Show.Document(document).ExecuteAsync();
    }
}
