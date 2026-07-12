using AFOCS.App.ViewModels;
using Caliburn.Micro;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;
using AFOCS.App.Communication;
using AFOCS.App.Devices;
using AFOCS.App.Devices.Implementation;
using AFOCS.App.Shared;
using Microsoft.Extensions.DependencyInjection.Extensions;
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


            RegisterDevices(services);

            _serviceProvider = services.BuildServiceProvider();
        }


        private void RegisterDevices(ServiceCollection services)
        {
            services.AddSingleton<GlueDispenserLeft>();
            services.AddSingleton<GlueDispenserRight>();
            services.AddSingleton<OpticalPowerMeterLeft>();
            services.AddSingleton<OpticalPowerMeterRight>();
            services.AddSingleton<ProgrammablePowerSupply>();
            services.AddSingleton<OpticalSwitch>();
            services.AddSingleton<CameraLight>();

            services.AddSingleton<IGlueDispenser>(sp => sp.GetService<GlueDispenserLeft>()!);
            services.AddSingleton<IGlueDispenser>(sp => sp.GetService<GlueDispenserRight>()!);
            services.AddSingleton<IOpticalPowerMeter>(sp => sp.GetService<OpticalPowerMeterLeft>()!);
            services.AddSingleton<IOpticalPowerMeter>(sp => sp.GetService<OpticalPowerMeterRight>()!);
            services.AddSingleton<IProgrammablePowerSupply>(sp => sp.GetService<ProgrammablePowerSupply>()!);
            services.AddSingleton<IOpticalSwitch>(sp => sp.GetService<OpticalSwitch>()!);
            services.AddSingleton<ICameraLight>(sp => sp.GetService<CameraLight>()!);

            services.AddSingleton<IDevice>(sp => sp.GetService<GlueDispenserLeft>()!);
            services.AddSingleton<IDevice>(sp => sp.GetService<GlueDispenserRight>()!);
            services.AddSingleton<IDevice>(sp => sp.GetService<OpticalPowerMeterLeft>()!);
            services.AddSingleton<IDevice>(sp => sp.GetService<OpticalPowerMeterRight>()!);
            services.AddSingleton<IDevice>(sp => sp.GetService<ProgrammablePowerSupply>()!);
            services.AddSingleton<IDevice>(sp => sp.GetService<OpticalSwitch>()!);
            services.AddSingleton<IDevice>(sp => sp.GetService<CameraLight>()!);

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