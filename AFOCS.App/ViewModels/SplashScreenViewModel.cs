using AFOCS.Devices;
using AFOCS.Devices.AkribrisMotion;
using AFOCS.Devices.BusAxisDevice;
using AFOCS.Devices.Camera;
using AFOCS.Devices.CameraLight;
using AFOCS.Devices.Gripper;
using AFOCS.Devices.HeightGauge;
using AFOCS.Devices.IO;
using AFOCS.Devices.IspBoard;
using AFOCS.Devices.MotionControlCard;
using AFOCS.Devices.OpticalPowerMeters;
using AFOCS.Devices.OpticalSwitch;
using AFOCS.Devices.PressureSensor;
using AFOCS.Devices.ProgrammablePowerSupply;
using AFOCS.Framework.Framework.Services;
using AFOCS.Infrastructure.Extensions;
using Caliburn.Micro;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Windows;

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
        public override string DisplayName { get; set; } = "AFOCS 设备初始化界面-----";
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

        // ---------------总线相关设备---------------------
        [Import] private IMotionControlCard _leadShineMotionCard = null!;
        [Import] private IBusAxisDevice _busAxisDevice = null!;
        [Import] private IIoDevice _ioDevice = null!;
        [ImportMany] private IEnumerable<IPressureSensor> _pressureSensors = null!;
        [ImportMany] private IEnumerable<IGripper> _grippers = null!;

        // ---------------其他设备---------------------
        [ImportMany] private IEnumerable<IAkribisMotion> _akribisMotions = null!;
        [ImportMany] private IEnumerable<IOpticalPowerMeter> _opticalPowerMeters = null!;
        [ImportMany] private IEnumerable<ICamera> _cameras = null!;

        [Import] private IOpticalSwitch _opticalSwitch = null!;
        [Import] private IHeightGauge _heightGauge = null!;
        [Import] private ICameraLight _cameraLight = null!;
        [Import] private ProgrammablePowerSupply _programmablePowerSupply = null!;

        // ---------------ISP Board---------------------
        [Import] private IIspBoardDevice _ispBoard = null!;
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
               _busAxisDevice,
                _ioDevice,
                .. _pressureSensors,
                .. _grippers,

                //.. _akribisMotions,
                //.. _opticalPowerMeters,
                .. _cameras,
                //_opticalSwitch,
                //_heightGauge,
                _cameraLight,
                _programmablePowerSupply,
            ];
            //添加 ISP Board
            //devices.Add(_ispBoard);

            return devices;
        }
    


        private string GetDeviceName(IDevice device)
        {
            return device.GetType().GetDescription();
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
