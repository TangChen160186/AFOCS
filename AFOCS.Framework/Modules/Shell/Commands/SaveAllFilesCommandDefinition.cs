using System.ComponentModel.Composition;
using System.Windows.Input;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Properties;

namespace AFOCS.Framework.Modules.Shell.Commands
{
    [CommandDefinition]
    public class SaveAllFilesCommandDefinition : CommandDefinition
    {
        public const string CommandName = "File.SaveAllFiles";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return Resources.FileSaveAllCommandText; }
        }

        public override string ToolTip
        {
            get { return Resources.FileSaveAllCommandToolTip; }
        }

        public override Uri IconSource
        {
            get 
            {
                return new Uri("pack://application:,,,/AFOCS.Framework;component/Resources/Icons/SaveAll.png"); 
            }
        }

        [Export]
        public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<SaveAllFilesCommandDefinition>(new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift));
    }
}