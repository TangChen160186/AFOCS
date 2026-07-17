using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO.Ports;
using AFOCS.App.Services;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Modules.Settings;
using AFOCS.Infrastructure;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CameraLightSettingsViewModel : PropertyChangedBase, ISettingsEditor
    {
        private readonly IConfigService _configService;
        private readonly CameraLight _cameraLight;
        private readonly IToastService _toastService;
        private CameraLightConfig _config = new();

        private string _portName = string.Empty;
        private int _baudRate = 19200;
        private bool _isBusy;
        private string _statusMessage = string.Empty;

        [ImportingConstructor]
        public CameraLightSettingsViewModel(
            IConfigService configService,
            CameraLight cameraLight,
            IToastService toastService)
        {
            _configService = configService;
            _cameraLight = cameraLight;
            _toastService = toastService;

            Channels = new ObservableCollection<LightChannelInfo>
            {
                new(CameraLightChannel.A, "通道 A"),
                new(CameraLightChannel.B, "通道 B"),
                new(CameraLightChannel.C, "通道 C"),
                new(CameraLightChannel.D, "通道 D"),
            };

            _ = LoadConfigAsync();
        }

        public string SettingsPageName => "相机光源";

        public string SettingsPagePath => "设备配置";

        // ========== 配置属性 ==========

        public string PortName
        {
            get => _portName;
            set
            {
                if (_portName == value) return;
                _portName = value;
                NotifyOfPropertyChange(() => PortName);
            }
        }

        public int BaudRate
        {
            get => _baudRate;
            set
            {
                if (_baudRate == value) return;
                _baudRate = value;
                NotifyOfPropertyChange(() => BaudRate);
            }
        }

        // ========== 连接状态 ==========

        public bool IsConnected => _cameraLight.IsConnected;

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                NotifyOfPropertyChange(() => IsBusy);
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                NotifyOfPropertyChange(() => StatusMessage);
            }
        }

        public void RefreshConnectionStatus()
        {
            NotifyOfPropertyChange(() => IsConnected);
            StatusMessage = IsConnected ? "已连接" : "未连接";
        }

        // ========== 端口扫描 ==========

        private ObservableCollection<string> _availablePorts = [];

        public ObservableCollection<string> AvailablePorts
        {
            get => _availablePorts;
            set
            {
                _availablePorts = value;
                NotifyOfPropertyChange(() => AvailablePorts);
            }
        }

        private bool _isScanning;

        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (_isScanning == value) return;
                _isScanning = value;
                NotifyOfPropertyChange(() => IsScanning);
            }
        }

        public async Task ScanPortsAsync()
        {
            IsScanning = true;
            try
            {
                var ports = await Task.Run(() => SerialPort.GetPortNames());
                AvailablePorts = new ObservableCollection<string>(ports);
            }
            finally
            {
                IsScanning = false;
            }
        }

        // ========== 操作 ==========

        public async Task ReconnectAsync()
        {
            IsBusy = true;
            StatusMessage = "正在重连...";
            try
            {
                SaveConfig();
                var result = await _cameraLight.ReConnectAsync();
                StatusMessage = result.IsSuccess ? "重连成功" : $"重连失败: {result.Message}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"重连异常: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                NotifyOfPropertyChange(() => IsConnected);
            }
        }

        public async Task DisconnectAsync()
        {
            IsBusy = true;
            StatusMessage = "正在断开...";
            try
            {
                var result = await _cameraLight.StopAsync();
                StatusMessage = result.IsSuccess ? "已断开" : $"断开失败: {result.Message}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"断开异常: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                NotifyOfPropertyChange(() => IsConnected);
            }
        }

        // ========== 通道控制 ==========

        public ObservableCollection<LightChannelInfo> Channels { get; }

        public async Task SetChannelBrightnessAsync(LightChannelInfo ch)
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            ch.IsBusy = true;
            try
            {
                var result = await _cameraLight.SetLightBrightnessAsync(ch.Channel, ch.Brightness);
                if (result.IsSuccess)
                    ch.AppliedBrightness = ch.Brightness;
            }
            finally
            {
                ch.IsBusy = false;
            }
        }

        // ========== ISettingsEditor ==========

        public void ApplyChanges()
        {
            SaveConfig();
        }

        private async Task LoadConfigAsync()
        {
            _config = await _configService.LoadAsync<CameraLightConfig>()
                      ?? new CameraLightConfig();
            _portName = _config.PortName;
            _baudRate = _config.BaudRate;

            NotifyOfPropertyChange(() => PortName);
            NotifyOfPropertyChange(() => BaudRate);
            RefreshConnectionStatus();
        }

        private void SaveConfig()
        {
            _config.PortName = _portName;
            _config.BaudRate = _baudRate;
            Task.Run(async () => await _configService.SaveAsync(_config));
        }
    }

    // ========== 光源通道信息 ==========

    public class LightChannelInfo : PropertyChangedBase
    {
        private uint _brightness = 128;
        private uint _appliedBrightness;
        private bool _isBusy;

        public LightChannelInfo(CameraLightChannel channel, string name)
        {
            Channel = channel;
            Name = name;
        }

        public CameraLightChannel Channel { get; }
        public string Name { get; }

        public uint Brightness
        {
            get => _brightness;
            set
            {
                if (_brightness == value) return;
                _brightness = Math.Clamp(value, 0u, 255u);
                NotifyOfPropertyChange();
            }
        }

        public uint AppliedBrightness
        {
            get => _appliedBrightness;
            set
            {
                if (_appliedBrightness == value) return;
                _appliedBrightness = value;
                NotifyOfPropertyChange();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                NotifyOfPropertyChange();
            }
        }
    }
}
