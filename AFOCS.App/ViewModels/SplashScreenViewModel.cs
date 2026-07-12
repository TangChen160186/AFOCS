using System.Collections.ObjectModel;
using System.Windows;
using AFOCS.App.Communication;
using AFOCS.App.Devices;
using AFOCS.App.Enums;
using AFOCS.App.Extensions;
using AFOCS.App.Shared;
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

        private int _progress;
        public int Progress
        {
            get => _progress;
            set => Set(ref _progress, value);
        }

        public string ProgressText => $"{Progress}%";

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        private string _loadingText = "初始化中...";
        public string LoadingText
        {
            get => _loadingText;
            set => Set(ref _loadingText, value);
        }

        private readonly ILogger<SplashScreenViewModel> _logger;
        private readonly IConfigService _configService;
        private readonly IEnumerable<IDevice> _devices;
        private readonly List<string> _errorMessages = new List<string>();

        public SplashScreenViewModel(ILogger<SplashScreenViewModel> logger, IConfigService configService,IEnumerable<IDevice> devices)
        {
            _logger = logger;
            _configService = configService;
            _devices = devices;
        }

        protected override Task OnActivatedAsync(CancellationToken cancellationToken)
        {
            InitializeDevices();
            return base.OnActivatedAsync(cancellationToken);
        }

        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
        }

        private async void InitializeDevices()
        {
            try
            {
                //await InitializeGlueDispensers();
                await InitializeOpticalPowerMeters();
                await InitializeProgrammablePowerSupply();
                await InitializeOpticalSwitch();
                await FinalizeInitialization();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化过程发生异常");
                AddLog(LogType.Error, $"系统初始化异常: {ex.Message}");
                _errorMessages.Add($"系统初始化异常: {ex.Message}");
                await ShowErrorDialogAndClose();
            }
        }


        private async Task InitializeGlueDispensers()
        {
            UpdateStatus("初始化点胶机...", 15);
            
            var tasks = new List<Task>();
            tasks.Add(InitializeDeviceAsync<IGlueDispenser>("点胶机", WorkPos.Left, 20));
            tasks.Add(InitializeDeviceAsync<IGlueDispenser>("点胶机", WorkPos.Right, 25));
            
            await Task.WhenAll(tasks);
            UpdateStatus("点胶机初始化完成", 30);
        }

        private async Task InitializeOpticalPowerMeters()
        {
            UpdateStatus("初始化光功率计...", 35);
            
            var tasks = new List<Task>();
            tasks.Add(InitializeDeviceAsync<IOpticalPowerMeter>("光功率计", WorkPos.Left, 42));
            tasks.Add(InitializeDeviceAsync<IOpticalPowerMeter>("光功率计", WorkPos.Right, 50));
            
            await Task.WhenAll(tasks);
            UpdateStatus("光功率计初始化完成", 55);
        }

        private async Task InitializeProgrammablePowerSupply()
        {
            UpdateStatus("初始化可编程电源...", 60);
            await InitializeDeviceAsync<IProgrammablePowerSupply>("可编程电源", WorkPos.Common, 70);
            UpdateStatus("可编程电源初始化完成", 75);
        }

        private async Task InitializeOpticalSwitch()
        {
            UpdateStatus("初始化光开关...", 80);
            await InitializeDeviceAsync<IOpticalSwitch>("光开关", WorkPos.Common, 90);
            UpdateStatus("光开关初始化完成", 95);
        }

        private async Task InitializeDeviceAsync<T>(string deviceName, WorkPos workPos, int targetProgress) where T : IDevice
        {
            string posText = workPos == WorkPos.Common ? "" : (workPos == WorkPos.Left ? "左工位" : "右工位");
            string displayName = $"{posText}{deviceName}".Trim();
            
            try
            {
                var device = IoC.Get<T>(workPos == WorkPos.Common ? null : workPos.GetName());
                var result = await device.InitializeAsync();
                
                if (result.IsSuccess)
                {
                    AddLog(LogType.Success, $"{displayName}初始化成功");
                }
                else
                {
                    AddLog(LogType.Error, $"{displayName}初始化失败: {result.Message}");
                    _errorMessages.Add($"{displayName}: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                AddLog(LogType.Error, $"{displayName}初始化异常: {ex.Message}");
                _errorMessages.Add($"{displayName}: {ex.Message}");
            }
            
            Progress = targetProgress;
            NotifyOfPropertyChange(() => ProgressText);
        }

        private async Task FinalizeInitialization()
        {
            UpdateStatus("完成初始化...", 98);
            await Task.Delay(300);
            
            Progress = 100;
            NotifyOfPropertyChange(() => ProgressText);
            
            IsLoading = false;
            LoadingText = "初始化完成";
            
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

        private void UpdateStatus(string status, int progress)
        {
            CurrentStatus = status;
            Progress = progress;
            NotifyOfPropertyChange(() => ProgressText);
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