using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Windows;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Devices.PressureSensor;
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

        [Import] private IWindowManager _windowManager = null!;
        [Import] private IMainWindow _mainWindow = null!;
        [Import] private ILogger _logger = null!;

        [Import] private ProgrammablePowerSupply _programmablePowerSupply = null!;
        [Import] private IOpticalSwitch _opticalSwitch = null!;
        [Import] private IHeightGauge _heightGauge = null!;
        [Import] private IMotionControlCard _leadShineMotionCard = null!;
        [Import] private IBusAxisDevice _busAxisDevice = null!;

        [Import] private ICameraLight _cameraLight = null!;
        [Import] private CameraLeftUp _cameraLeftUp = null!;
        [Import] private CameraLeftDown _cameraLeftDown = null!;
        [Import] private CameraRightUp _cameraRightUp = null!;
        [Import] private CameraRightDown _cameraRightDown = null!;
        [Import] private OpticalPowerMeterLeft _opticalPowerMeterLeft = null!;
        [Import] private OpticalPowerMeterRight _opticalPowerMeterRight = null!;

        [Import] private ISPBoardDevice _boardDevice = null!;

        [Import] private LeftCouplingLGripper _leftCouplingLGripper = null!;
        [Import] private LeftCouplingRGripper _leftCouplingRGripper = null!;
        [Import] private RightCouplingLGripper _rightCouplingLGripper = null!;
        [Import] private RightCouplingRGripper _rightCouplingRGripper = null!;


        [Import] private IIODevice _ioDevice = null!;

        [Import] private LeftCouplingLPressureSensor _leftCouplingLPressure = null!;
        [Import] private LeftCouplingRPressureSensor _leftCouplingRPressure = null!;
        [Import] private LeftDispensePressureSensor _leftDispensePressure = null!;
        [Import] private RightCouplingLPressureSensor _rightCouplingLPressure = null!;
        [Import] private RightCouplingRPressureSensor _rightCouplingRPressure = null!;
        [Import] private RightDispensePressureSensor _rightDispensePressure = null!;

        [Import] private AkribisLeftCouplingL _arAkribisLeftCouplingL = null!;
        [Import] private AkribisLeftCouplingR _akribisLeftCouplingR = null!;
        [Import] private AkribisRightCouplingL _akribisRightCouplingL = null!;
        [Import] private AkribisRightCouplingR _akribisRightCouplingR = null!;
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
                _leadShineMotionCard,
                //_busAxisDevice,
                //_ioDevice,

                //_leftCouplingLGripper,
                //_leftCouplingRGripper,
                //_rightCouplingLGripper,
                //_rightCouplingRGripper,

                _leftCouplingLPressure,
                _leftCouplingRPressure,
                _leftDispensePressure,
                _rightCouplingLPressure,
                _rightCouplingRPressure,
                _rightDispensePressure,

                _arAkribisLeftCouplingL,
                _akribisLeftCouplingR,
                _akribisRightCouplingL,
                _akribisRightCouplingR,

                _programmablePowerSupply,

                //_opticalSwitch,
                //_heightGauge,
                //_opticalPowerMeterLeft,
                //_opticalPowerMeterRight,

                _cameraLight,

                //_cameraLeftUp,
                //_cameraLeftDown,
                //_cameraRightUp,
                //_cameraRightDown,


            ];
            return devices;
        }
    


        private string GetDeviceName(IDevice device)
        {
            return device switch
            {
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
                BusAxisDevice => "总线轴设备",
                IODevice => "IO 设备",
                LeftCouplingLGripper => "左耦合左夹爪",
                LeftCouplingRGripper => "左耦合右夹爪",
                RightCouplingLGripper => "右耦合左夹爪",
                RightCouplingRGripper => "右耦合右夹爪",
                LeftCouplingLPressureSensor => "左工位左耦合压力传感器",
                LeftCouplingRPressureSensor => "左工位右耦合压力传感器",
                LeftDispensePressureSensor => "左工位点胶压力传感器",
                RightCouplingLPressureSensor => "右工位左耦合压力传感器",
                RightCouplingRPressureSensor => "右工位右耦合压力传感器",
                RightDispensePressureSensor => "右工位点胶压力传感器",
                LeftCouplingLConfig => "左工位左耦合轴",
                LeftCouplingRConfig => "左工位右耦合轴",
                RightCouplingLConfig => "右工位左耦合轴",
                RightCouplingRConfig => "右工位右耦合轴",
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
