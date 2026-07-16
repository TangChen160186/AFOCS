using System.Windows;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Threading;

namespace AFOCS.Framework.Modules.Shell.Commands
{
    [CommandHandler]
    public class ViewFullScreenCommandHandler : CommandHandlerBase<ViewFullScreenCommandDefinition>
    {
        public override Task Run(Command command)
        {
            var window = Application.Current.MainWindow;
            if (window == null)
                return TaskUtility.Completed;
            if (window.WindowState != WindowState.Maximized)
                window.WindowState = WindowState.Maximized;
            else
                window.WindowState = WindowState.Normal;
            return TaskUtility.Completed;
        }
    }
}