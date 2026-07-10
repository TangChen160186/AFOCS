using AFOCS.App.ViewModels;
using Caliburn.Micro;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;
using AFOCS.App.Communication;
using AFOCS.App.Devices;
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

            // 注册Microsoft日志系统
            services.AddLogging(logBuilder =>
            {
                logBuilder.SetMinimumLevel(LogLevel.Debug);

                // 配置控制台彩色输出
                logBuilder.AddConsole(consoleOpts =>
                    {
                        consoleOpts.FormatterName = ConsoleFormatterNames.Simple;
                    })
                    .AddSimpleConsole(simpleOpts =>
                    {
                        simpleOpts.ColorBehavior = LoggerColorBehavior.Enabled;
                    });
            });


            // 注册Caliburn.Micro核心组件（必须）
            services.AddSingleton<IWindowManager, WindowManager>();
            services.AddSingleton<IEventAggregator, EventAggregator>();

            // 注册你的所有ViewModel
            services.AddTransient<SplashScreenViewModel>();


            // 注册通信服务
            services.AddTransient<ISerialPortClient, SerialPortClient>();
            services.AddTransient<ITcpClient, TcpClient>();

            // 注册配置服务
            services.AddTransient<IConfigService,ConfigService>();


            // 注册设备服务
            services.AddKeyedSingleton<IGlueDispenser>(nameof(WorkPos.Left), (provider, o) => new GlueDispenser(WorkPos.Left));
            services.AddKeyedSingleton<IGlueDispenser>(nameof(WorkPos.Right), (provider, o) => new GlueDispenser(WorkPos.Right));

            services.AddKeyedSingleton<IOpticalPowerMeter>(nameof(WorkPos.Left), (provider, o) => new OpticalPowerMeter(WorkPos.Left));
            services.AddKeyedSingleton<IOpticalPowerMeter>(nameof(WorkPos.Right), (provider, o) => new OpticalPowerMeter(WorkPos.Right));

            services.AddKeyedSingleton<IProgrammablePowerSupply>(nameof(WorkPos.Left), (provider, o) => new ProgrammablePowerSupply(WorkPos.Left));
            services.AddKeyedSingleton<IProgrammablePowerSupply>(nameof(WorkPos.Right), (provider, o) => new ProgrammablePowerSupply(WorkPos.Right));

            // 构建容器
            _serviceProvider = services.BuildServiceProvider();
        }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            DisplayRootViewForAsync<SplashScreenViewModel>();
        }

        // 2. CM创建实例时走DI容器
        protected override object GetInstance(Type service, string key)
        {
            return _serviceProvider.GetRequiredKeyedService(service, key);
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
            // 记录日志或显示错误信息
            MessageBox.Show("程序发生了一个可恢复的错误，请重试。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
            base.OnUnhandledException(sender, e);
        }


        private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            // 记录日志或显示错误信息
        }
        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // 记录日志或显示错误信息
            Console.WriteLine(e);
            e.SetObserved();
        }
    }
}
