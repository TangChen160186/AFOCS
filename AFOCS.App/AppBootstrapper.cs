using AFOCS.App.ViewModels;
using Caliburn.Micro;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;
using AFOCS.App.Communication;
using AFOCS.App.Devices;
using AFOCS.App.Devices.Implementation;
using AFOCS.App.Enums;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace AFOCS.App
{
    internal class AppBootstrapper : BootstrapperBase
    {
        private IServiceProvider _serviceProvider = null!;
        
        public AppBootstrapper()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            Initialize();
        }
       
        protected override void Configure()
        {
            var services = new ServiceCollection();

            services.AddLogging(logBuilder =>
            {
                logBuilder.SetMinimumLevel(LogLevel.Debug);
                logBuilder.AddConsole(consoleOpts =>
                    {
                        consoleOpts.FormatterName = ConsoleFormatterNames.Simple;
                    })
                    .AddSimpleConsole(simpleOpts =>
                    {
                        simpleOpts.ColorBehavior = LoggerColorBehavior.Enabled;
                    });
            });

            services.AddSingleton<IWindowManager, WindowManager>();
            services.AddSingleton<IEventAggregator, EventAggregator>();

            services.AddTransient<SplashScreenViewModel>();
            services.AddTransient<MainWindowViewModel>();

            services.AddTransient<ISerialPortClient, SerialPortClient>();
            services.AddTransient<ITcpClient, TcpClient>();

            services.AddTransient<IConfigService, ConfigService>();

            // now use io write 
            services.AddKeyedSingleton<IGlueDispenser>(nameof(WorkPos.Left),
                (provider, o) => new GlueDispenser(WorkPos.Left,
                    provider.GetRequiredService<ISerialPortClient>(),
                    provider.GetRequiredService<IConfigService>(),
                    provider.GetRequiredService<ILogger<GlueDispenser>>()));
            services.AddKeyedSingleton<IGlueDispenser>(nameof(WorkPos.Right),
                (provider, o) => new GlueDispenser(WorkPos.Right,
                    provider.GetRequiredService<ISerialPortClient>(),
                    provider.GetRequiredService<IConfigService>(),
                    provider.GetRequiredService<ILogger<GlueDispenser>>()));

            services.AddKeyedSingleton<IOpticalPowerMeter>(nameof(WorkPos.Left),
                (provider, o) => new OpticalPowerMeter(WorkPos.Left,
                    provider.GetRequiredService<ITcpClient>(),
                    provider.GetRequiredService<IConfigService>(),
                    provider.GetRequiredService<ILogger<OpticalPowerMeter>>()));

            services.AddKeyedSingleton<IOpticalPowerMeter>(nameof(WorkPos.Right),
                (provider, o) => new OpticalPowerMeter(WorkPos.Right,
                    provider.GetRequiredService<ITcpClient>(),
                    provider.GetRequiredService<IConfigService>(),
                    provider.GetRequiredService<ILogger<OpticalPowerMeter>>()));
            //services.AddSingleton<IIoController, IIoController>();
            //services.AddSingleton<IMotionController, IIoController>();
            services.AddSingleton<ICameraLight, CameraLight>();
            services.AddSingleton<IProgrammablePowerSupply, ProgrammablePowerSupply>();
            services.AddSingleton<IOpticalSwitch, OpticalSwitch>();

            _serviceProvider = services.BuildServiceProvider();
        }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            DisplayRootViewForAsync<SplashScreenViewModel>();
        }

        protected override object GetInstance(Type service, string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                return _serviceProvider.GetRequiredKeyedService(service, key);
            }
            return _serviceProvider.GetRequiredService(service);
        }

        protected override IEnumerable<object?> GetAllInstances(Type service)
        {
            return _serviceProvider.GetServices(service);
        }

        protected override void BuildUp(object instance)
        {
        }

        protected override void OnExit(object sender, EventArgs e)
        {
            Console.WriteLine("Application is exiting.");
            base.OnExit(sender, e);
        }

        protected override void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("程序发生了一个可恢复的错误，请重试。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
            base.OnUnhandledException(sender, e);
        }

        private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"未处理的异常: {e.ExceptionObject}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        
        }
        
        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            MessageBox.Show($"未观察到的任务异常: {e.Exception}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.SetObserved();
        }
    }
}