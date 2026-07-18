using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using AFOCS.Framework;
using AFOCS.Framework.Framework.Services;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace AFOCS.App
{
    internal class Bootstrapper: AppBootstrapper
    {
        protected override void OnStartup(object sender, System.Windows.StartupEventArgs e)
        {
            // 初始化 NodeEditor 的静态定位器
            AFOCS.FlowNodeEditor.AppBootstrapper.Initialize(Container);

            DisplayRootViewForAsync<ViewModels.SplashScreenViewModel>();
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
