using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Properties;

namespace AFOCS.Framework.Modules.Shell.Commands
{
    [CommandDefinition]
    public class CloseFileCommandDefinition : CommandDefinition
    {
        public const string CommandName = "File.CloseFile";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return Resources.FileCloseCommandText; }
        }

        public override string ToolTip
        {
            get { return Resources.FileCloseCommandToolTip; }
        }
    }
}