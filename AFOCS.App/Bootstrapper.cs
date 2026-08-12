using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Windows;
using System.Windows.Threading;
using AFOCS.Devices;
using AFOCS.Devices.AkribrisMotion;
using AFOCS.Devices.BusAxisDevice;
using AFOCS.Devices.Camera;
using AFOCS.Devices.CameraLight;
using AFOCS.Devices.Gripper;
using AFOCS.Devices.HeightGauge;
using AFOCS.Devices.IO;
using AFOCS.Devices.MotionControlCard;
using AFOCS.Devices.OpticalPowerMeters;
using AFOCS.Devices.OpticalSwitch;
using AFOCS.Devices.PressureSensor;
using AFOCS.Devices.ProgrammablePowerSupply;
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

    // 关闭应用前释放所有已初始化的设备资源
    protected override void OnExit(object sender, EventArgs e)
    {
        base.OnExit(sender, e);
        DisposeDevices();
    }

    /// <summary>
    /// 从 MEF 容器取回与 SplashScreen 初始化一致的设备集合，逆序释放资源。
    /// 每个设备单独 try-catch，避免单个设备释放失败影响其它设备及程序退出。
    /// </summary>
    private void DisposeDevices()
    {
        var contractTypes = new[]
        {
            typeof(IMotionControlCard),
            typeof(IBusAxisDevice),
            typeof(IIoDevice),
            typeof(IPressureSensor),
            typeof(IGripper),
            typeof(IAkribisMotion),
            typeof(IOpticalPowerMeter),
            typeof(ICamera),
            typeof(IOpticalSwitch),
            typeof(IHeightGauge),
            typeof(ICameraLight),
            typeof(ProgrammablePowerSupply),
        };

        var disposables = new List<IDevice>();
        foreach (var type in contractTypes.Reverse()) // 逆序：后初始化的设备先释放
        {
            try
            {
                var contractName = AttributedModelServices.GetContractName(type);
                foreach (var value in Container.GetExportedValues<object>(contractName))
                {
                    if (value is IDevice device && !disposables.Contains(device))
                        disposables.Add(device);
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "获取设备 {Type} 失败", type.Name);
            }
        }

        foreach (var device in disposables)
        {
            try
            {
                _logger?.Information("[{Device}] 正在释放资源...", device.GetType().Name);
                device.Dispose();
                _logger?.Information("[{Device}] 资源释放完成", device.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "[{Device}] 释放资源失败", device.GetType().Name);
            }
        }
    }
}