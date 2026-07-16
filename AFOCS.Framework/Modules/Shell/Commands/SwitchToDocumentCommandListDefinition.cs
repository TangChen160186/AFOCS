using AFOCS.Framework.Framework.Commands;

namespace AFOCS.Framework.Modules.Shell.Commands
{
    [CommandDefinition]
    public class SwitchToDocumentCommandListDefinition : CommandListDefinition
    {
        public const string CommandName = "Window.SwitchToDocument";

        public override string Name
        {
            get { return CommandName; }
        }
    }
}