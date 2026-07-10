using System.Windows;

namespace AFOCS.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var bootstrapper = new AppBootstrapper();
            base.OnStartup(e);
        }
    }

}
