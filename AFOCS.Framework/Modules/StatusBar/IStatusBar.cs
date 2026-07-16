using System.Windows;
using AFOCS.Framework.Modules.StatusBar.ViewModels;
using Caliburn.Micro;

namespace AFOCS.Framework.Modules.StatusBar
{
    public interface IStatusBar
    {
        IObservableCollection<StatusBarItemViewModel> Items { get; }

        void AddItem(string message, GridLength width);
    }
}
