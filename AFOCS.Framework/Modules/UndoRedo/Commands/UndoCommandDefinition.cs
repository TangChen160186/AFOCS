using System.ComponentModel.Composition;
using System.Windows.Input;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Properties;

namespace AFOCS.Framework.Modules.UndoRedo.Commands
{
    [CommandDefinition]
    public class UndoCommandDefinition : CommandDefinition
    {
        public const string CommandName = "Edit.Undo";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return Resources.EditUndoCommandText; }
        }

        public override string ToolTip
        {
            get { return Resources.EditUndoCommandToolTip; }
        }

        public override Uri IconSource
        {
            get { return new Uri("pack://application:,,,/AFOCS.Framework;component/Resources/Icons/Undo.png"); }
        }

        [Export]
        public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<UndoCommandDefinition>(new KeyGesture(Key.Z, ModifierKeys.Control));
    }
}