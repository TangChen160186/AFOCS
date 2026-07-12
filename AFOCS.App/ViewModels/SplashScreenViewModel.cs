using System.Collections.ObjectModel;
using System.Windows;
using AFOCS.App.Devices;
using AFOCS.App.Devices.Implementation;
using Caliburn.Micro;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.ViewModels
{
    public enum LogType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class LogMessage
    {
        public LogType Type { get; set; }
        public string Time { get; set; }
        public string Message { get; set; }
    }

    internal class SplashScreenViewModel : Screen
    {
        public ObservableCollection<LogMessage> LogMessages { get; } = new ObservableCollection<LogMessage>();

        private string _currentStatus = "正在初始化系统...";
        public string CurrentStatus
        {
            get => _currentStatus;
            set => Set(ref _currentStatus, value);
        }

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        private readonly ILogger<SplashScreenViewModel> _logger;
        private readonly IEnumerable<IDevice> _devices;
        private readonly List<string> _errorMessages = new List<string>();

        public SplashScreenViewModel(ILogger<SplashScreenViewModel> logger, IEnumerable<IDevice> devices)
        {
            _logger = logger;
            _devices = devices;
        }

        protected override Task OnActivatedAsync(CancellationToken cancellationToken)
        {
            InitializeDevices();
            return base.OnActivatedAsync(cancellationToken);
        }

        private async void InitializeDevices()
        {
            try
            {
                var deviceList = _devices.ToList();
                UpdateStatus("正在初始化设备...");

                for (int i = 0; i < deviceList.Count; i++)
                {
                    var device = deviceList[i];
                    var deviceName = GetDeviceName(device);
                    UpdateStatus($"正在初始化{deviceName}...");

                    try
                    {
                        var result = await device.InitializeAsync();

                        if (result.IsSuccess)
                        {
                            AddLog(LogType.Success, $"{deviceName}初始化成功");
                        }
                        else
                        {
                            AddLog(LogType.Error, $"{deviceName}初始化失败: {result.Message}");
                            _errorMessages.Add($"{deviceName}: {result.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog(LogType.Error, $"{deviceName}初始化异常: {ex.Message}");
                        _errorMessages.Add($"{deviceName}: {ex.Message}");
                    }
                }

                await FinalizeInitialization();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化过程发生异常");
                AddLog(LogType.Error, $"系统初始化异常: {ex.Message}");
                _errorMessages.Add($"系统初始化异常: {ex.Message}");
            }
        }

        private string GetDeviceName(IDevice device)
        {
            return device switch
            {
                GlueDispenserLeft => "左工位点胶机",
                GlueDispenserRight => "右工位点胶机",
                OpticalPowerMeterLeft => "左工位光功率计",
                OpticalPowerMeterRight => "右工位光功率计",
                ProgrammablePowerSupply => "可编程电源",
                OpticalSwitch => "光开关",
                CameraLight => "相机光源",
                HeightGauge => "测高仪",
                _ => device.GetType().Name
            };
        }

        private async Task FinalizeInitialization()
        {
            UpdateStatus("完成初始化...");
            IsLoading = false;
            await Task.Delay(500);
            
            if (_errorMessages.Count > 0)
            {
                await ShowErrorDialogAndClose();
            }
            else
            {
                NavigateToMainWindow();
            }
        }

        private async Task ShowErrorDialogAndClose()
        {
            string errorSummary = string.Join("\n", _errorMessages);
            string message = $"检测到以下设备初始化失败：\n\n{errorSummary}\n\n是否继续进入主界面？";
            
            var result = MessageBox.Show(message, "设备初始化警告", 
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                NavigateToMainWindow();
            }
            else
            {
                await TryCloseAsync();
                Application.Current.Shutdown();
            }
        }

        private void NavigateToMainWindow()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    var windowManager = IoC.Get<IWindowManager>();
                    var mainViewModel = IoC.Get<MainWindowViewModel>();
                    await windowManager.ShowWindowAsync(mainViewModel);
                    await TryCloseAsync();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "打开主窗口失败");
                MessageBox.Show($"打开主窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private void UpdateStatus(string status)
        {
            CurrentStatus = status;
        }

        private void AddLog(LogType type, string message)
        {
            LogMessages.Add(new LogMessage
            {
                Type = type,
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Message = message
            });
            
            _logger.LogDebug($"[{type}] {message}");
        }
    }
}