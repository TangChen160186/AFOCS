using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Windows;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Framework.Services;
using Caliburn.Micro;
using Serilog;

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
    [Export]
    internal class SplashScreenViewModel : Screen
    {
        public override string DisplayName { get; set; } = "AFOCS 初始化设备初始化界面";
        public ObservableCollection<LogMessage> LogMessages { get; } = [];

        public string CurrentStatus
        {
            get;
            set => Set(ref field, value);
        } = "正在初始化系统...";

        public bool IsLoading
        {
            get;
            set => Set(ref field, value);
        } = true;


        private readonly List<string> _errorMessages = [];

        [Import] private ILogger _logger = null!;

        [Import] private ProgrammablePowerSupply _programmablePowerSupply = null!;
        [Import] private OpticalSwitch _opticalSwitch = null!;
        [Import] private HeightGauge _heightGauge = null!;
        [Import] private LeadShineMotionCard _leadShineMotionCard = null!;

        [Import] private GlueDispenserLeft _glueDispenserLeft = null!;
        [Import] private GlueDispenserRight _glueDispenserRight = null!;
        [Import] private CameraLight _cameraLight = null!;
        [Import] private CameraLeftUp _cameraLeftUp = null!;
        [Import] private CameraLeftDown _cameraLeftDown = null!;
        [Import] private CameraRightUp _cameraRightUp = null!;
        [Import] private CameraRightDown _cameraRightDown = null!;
        [Import] private OpticalPowerMeterLeft _opticalPowerMeterLeft = null!;
        [Import] private OpticalPowerMeterRight _opticalPowerMeterRight = null!;


        [Import] private IWindowManager _windowManager = null!;

        [Import] private IMainWindow _mainWindow = null!;
        protected override Task OnActivatedAsync(CancellationToken cancellationToken)
        {
            InitializeDevices();
            return base.OnActivatedAsync(cancellationToken);
        }

        private async void InitializeDevices()
        {
            try
            {
                UpdateStatus("正在初始化设备...");
                List<IDevice> devices = GetAllDevices();
                foreach (var device in devices)
                {
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
                _logger.Error(ex, "初始化过程发生异常");
                AddLog(LogType.Error, $"系统初始化异常: {ex.Message}");
                _errorMessages.Add($"系统初始化异常: {ex.Message}");
            }
        }

        private List<IDevice> GetAllDevices()
        {

            List<IDevice> devices =
            [
                _programmablePowerSupply,
                //_opticalSwitch,
                //_heightGauge,
                //_glueDispenserLeft,
                //_glueDispenserRight,
                //_opticalPowerMeterLeft,
                //_opticalPowerMeterRight,
                //_cameraLight,

                //_cameraLeftUp,
                //_cameraLeftDown,
                //_cameraRightUp,
                //_cameraRightDown,
                //_leadShineMotionCard,
            ];
            return devices;
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
                CameraLeftDown => "左下相机",
                CameraRightDown => "右下相机",
                CameraLeftUp => "左上相机",
                CameraRightUp => "右上相机",
                LeadShineMotionCard => "雷赛控制卡",
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
                    _mainWindow.Title = "DEMISION AFOCS";
                    await _windowManager.ShowWindowAsync(_mainWindow);
                    await TryCloseAsync();
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "打开主窗口失败");
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
            
            _logger.Debug($"[{type}] {message}");
        }
    }
}