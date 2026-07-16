using AFOCS.Framework.Framework.Commands;

namespace AFOCS.Framework.Test.Modules.Test.Commands
{
    [CommandDefinition]
    public class ViewHomeCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.Home";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "Home"; }
        }

        public override string ToolTip
        {
            get { return "Home"; }
        }
    }
}