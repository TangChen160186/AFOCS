using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using AFOCS.Framework;
using System.Windows;
using AFOCS.App.ViewModels;
using AFOCS.Framework.Framework.Services;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace AFOCS.App
{
    internal class Bootstrapper: AppBootstrapper
    {
        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            DisplayRootViewForAsync<SplashScreenViewModel>();
        }

        protected override void BindServices(CompositionBatch batch)
        {
            base.BindServices(batch);

            ILogger logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}",
                    theme: SystemConsoleTheme.Colored)
                .CreateLogger();
            batch.AddExportedValue<ILogger>(logger);
        }
    }
}
