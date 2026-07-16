using System.Windows;

namespace AFOCS.App.Services
{
    public interface IToastService
    {
        void ShowInfo(string message);
        void ShowWarning(string message);
        void ShowError(string message);
    }

    [System.ComponentModel.Composition.Export(typeof(IToastService))]
    public class ToastService : IToastService
    {
        public void ShowInfo(string message)
        {
            MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowWarning(string message)
        {
            MessageBox.Show(message, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowError(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
