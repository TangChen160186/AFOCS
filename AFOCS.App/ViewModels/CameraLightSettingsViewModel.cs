using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using AFOCS.App.Services;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Modules.Settings;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CameraLightSettingsViewModel : Screen, ISettingsEditor
    {
        private readonly ICameraLight _cameraLight;
        private readonly IToastService _toastService;
        private readonly CameraLightConfig _config = new();
        private bool _isModify;

        private readonly string[] _modifyProperties =
        [
            nameof(PortName), nameof(BaudRate), nameof(TimeoutMs),
        ];

        [ImportingConstructor]
        public CameraLightSettingsViewModel(ICameraLight cameraLight, IToastService toastService)
        {
            _cameraLight = cameraLight;
            _toastService = toastService;

            Channels = new ObservableCollection<LightChannelInfo>
            {
                new(CameraLightChannel.A, "通道 A"),
                new(CameraLightChannel.B, "通道 B"),
                new(CameraLightChannel.C, "通道 C"),
                new(CameraLightChannel.D, "通道 D"),
            };

            var config = _cameraLight.GetConfig();
            _config.PortName = config.PortName;
            _config.BaudRate = config.BaudRate;
            _config.TimeoutMs = config.TimeoutMs;
        }

        protected override void OnViewAttached(object view, object context)
        {
            base.OnViewAttached(view, context);
            _ = ScanPortsAsync();
        }

        public string SettingsPageName => "相机光源";
        public string SettingsPagePath => "设备配置";

        // ========== 配置属性 ==========

        public string PortName
        {
            get => _config.PortName;
            set
            {
                if (_config.PortName == value) return;
                _config.PortName = value;
                NotifyOfPropertyChange();
            }
        }

        public int BaudRate
        {
            get => _config.BaudRate;
            set
            {
                if (_config.BaudRate == value) return;
                _config.BaudRate = value;
                NotifyOfPropertyChange();
            }
        }

        public int TimeoutMs
        {
            get => _config.TimeoutMs;
            set
            {
                if (_config.TimeoutMs == value) return;
                _config.TimeoutMs = value;
                NotifyOfPropertyChange();
            }
        }

        // ========== 连接状态 ==========

        public bool IsConnected => _cameraLight.IsConnected;

        public bool IsBusy
        {
            get;
            set => Set(ref field, value);
        }

        public string StatusMessage
        {
            get;
            set => Set(ref field, value);
        } = string.Empty;

        public void RefreshConnectionStatus()
        {
            NotifyOfPropertyChange(() => IsConnected);
            StatusMessage = IsConnected ? "已连接" : "未连接";
        }

        // ========== 端口扫描 ==========

        public ObservableCollection<string> AvailablePorts
        {
            get;
            set => Set(ref field, value);
        } = [];

        public bool IsScanning
        {
            get;
            set => Set(ref field, value);
        }

        public async Task ScanPortsAsync()
        {
            IsScanning = true;
            try
            {
                var ports = await Task.Run(SerialPort.GetPortNames);
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
                if (_isModify)
                {
                    await _cameraLight.SaveConfigAsync(_config);
                    _isModify = false;
                }
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

        public async Task SaveAsync()
        {


            IsBusy = true;
            StatusMessage = "正在保存...";
            try
            {
                await _cameraLight.SaveConfigAsync(_config);
                _isModify = false;
                StatusMessage = "配置已保存";
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存异常: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
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

        public override void NotifyOfPropertyChange([CallerMemberName] string? propertyName = null)
        {
            base.NotifyOfPropertyChange(propertyName);

            if (_modifyProperties.Contains(propertyName))
            {
                _isModify = true;
            }
        }

        public void ApplyChanges()
        {
            if (!_isModify) return;
            _ = SaveAsync();
        }
    }

    // ========== 光源通道信息 ==========

    public class LightChannelInfo : PropertyChangedBase
    {
        public LightChannelInfo(CameraLightChannel channel, string name)
        {
            Channel = channel;
            Name = name;
        }

        public CameraLightChannel Channel { get; }
        public string Name { get; }

        public uint Brightness
        {
            get;
            set
            {
                field = Math.Clamp(value, 0u, 255u);
                NotifyOfPropertyChange();
            }
        } = 128;

        public uint AppliedBrightness
        {
            get;
            set => Set(ref field, value);
        }

        public bool IsBusy
        {
            get;
            set => Set(ref field, value);
        }
    }
}
