using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Windows;
using System.Windows.Threading;
using AFOCS.Framework;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace AFOCS.App;

internal class Bootstrapper: AppBootstrapper
{
    private ILogger _logger;
    public Bootstrapper()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            _logger?.Fatal(ex, "发生致命的未处理异常");
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            _logger?.Error(args.Exception, "捕获到未观察的Task异常");
            args.SetObserved();
        };
    }
    protected override void OnStartup(object sender, System.Windows.StartupEventArgs e)
    {
        DisplayRootViewForAsync<ViewModels.SplashScreenViewModel>();
    }
    // 重写OnUnhandledException处理UI线程异常
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


    protected override void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error(e.Exception, "捕获到未处理的UI线程异常");
        MessageBox.Show($"发生错误: {e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // 阻止程序崩溃
    }

}