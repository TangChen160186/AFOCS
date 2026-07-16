using AFOCS.App.Services;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Framework.Services;
using AFOCS.Framework.Modules.Settings;
using AFOCS.Infrastructure;
using Caliburn.Micro;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;

namespace AFOCS.App.ViewModels
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class PowerSupplySettingsViewModel : PropertyChangedBase, ISettingsEditor
    {
        private readonly IConfigService _configService;
        private readonly ProgrammablePowerSupply _powerSupply;
        private ProgrammablePowerSupplyConfig _config = new();

        private string _visaAddress = string.Empty;
        private int _timeoutMs;
        private ObservableCollection<string> _availableResources = [];
        private bool _isScanning;
        private bool _isBusy;
        private string _statusMessage = string.Empty;

        private IToastService _toastService;

        [ImportingConstructor]
        public PowerSupplySettingsViewModel(IConfigService configService, ProgrammablePowerSupply powerSupply, IToastService toastService)
        {
            _configService = configService;
            _powerSupply = powerSupply;
            _toastService = toastService;

            Channels = new ObservableCollection<ChannelInfo>
            {
                new(1), new(2)
            };

            _ = LoadConfigAsync();
        }

        public string SettingsPageName => "可编程电源";

        public string SettingsPagePath => "设备配置";

        // ========== 配置属性 ==========

        public string VisaAddress
        {
            get => _visaAddress;
            set
            {
                if (_visaAddress == value) return;
                _visaAddress = value;
                NotifyOfPropertyChange(() => VisaAddress);
            }
        }

        public int TimeoutMs
        {
            get => _timeoutMs;
            set
            {
                if (_timeoutMs == value) return;
                _timeoutMs = value;
                NotifyOfPropertyChange(() => TimeoutMs);
            }
        }

        // ========== 连接状态 ==========

        public bool IsConnected => _powerSupply.IsConnected;

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

        // ========== 操作 ==========

        public async Task ReconnectAsync()
        {
            IsBusy = true;
            StatusMessage = "正在重连...";
            try
            {
                // 先保存当前配置，再重连
                _config.VisaAddress = _visaAddress;
                _config.TimeoutMs = _timeoutMs;
                await _configService.SaveAsync(_config);

                var result = await _powerSupply.ReConnectAsync();
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
                var result = await _powerSupply.StopAsync();
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

        // ========== 扫描 ==========

        public ObservableCollection<string> AvailableResources
        {
            get => _availableResources;
            set
            {
                _availableResources = value;
                NotifyOfPropertyChange(() => AvailableResources);
            }
        }

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

        public async Task ScanAvailableResources()
        {
            IsScanning = true;
            try
            {
                var resources = await Task.Run(() => ProgrammablePowerSupply.GetAvailableResources());
                AvailableResources = new ObservableCollection<string>(resources);
            }
            finally
            {
                IsScanning = false;
            }
        }

        // ========== 通道测试 ==========

        public ObservableCollection<ChannelInfo> Channels { get; }

        public async Task ReadChannelAsync(ChannelInfo ch)
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            ch.IsBusy = true;
            try
            {
                var vResult = await _powerSupply.GetVoltageAndCurrentAsync(ch.Number);
                if (vResult.IsSuccess)
                {
                    ch.ActualVoltage = vResult.Data.Item1;
                    ch.ActualCurrent = vResult.Data.Item2;
                }

                var sResult = await _powerSupply.GetChannelStatusAsync(ch.Number);
                if (sResult.IsSuccess)
                    ch.IsEnabled = sResult.Data;
            }
            finally
            {
                ch.IsBusy = false;
            }
        }

        public async Task ApplyChannelAsync(ChannelInfo ch)
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            ch.IsBusy = true;
            try
            {
                await _powerSupply.SetVoltageAndCurrentAsync(ch.Number, ch.TargetVoltage, ch.TargetCurrent);
                await _powerSupply.SetChannelStatusAsync(ch.Number, ch.IsEnabled);
                await ReadChannelAsync(ch);
            }
            finally
            {
                ch.IsBusy = false;
            }
        }

        public async Task ToggleChannelAsync(ChannelInfo ch)
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            ch.IsBusy = true;
            try
            {
                var sResult = await _powerSupply.GetChannelStatusAsync(ch.Number);
                if (sResult.IsSuccess)
                {
                    bool newState = !sResult.Data;
                    await _powerSupply.SetChannelStatusAsync(ch.Number, newState);
                    ch.IsEnabled = newState;
                }
            }
            finally
            {
                ch.IsBusy = false;
            }
        }

        public async Task ReadAllChannelsAsync()
        {
            foreach (var ch in Channels)
                await ReadChannelAsync(ch);
        }

        // ========== ISettingsEditor ==========

        public void ApplyChanges()
        {
            _config.VisaAddress = _visaAddress;
            _config.TimeoutMs = _timeoutMs;
            Task.Run(async () => await _configService.SaveAsync(_config));
        }

        private async Task LoadConfigAsync()
        {
            _config = await _configService.LoadAsync<ProgrammablePowerSupplyConfig>()
                      ?? new ProgrammablePowerSupplyConfig();
            _visaAddress = _config.VisaAddress;
            _timeoutMs = _config.TimeoutMs;

            NotifyOfPropertyChange(() => VisaAddress);
            NotifyOfPropertyChange(() => TimeoutMs);
            RefreshConnectionStatus();
            _ = ReadAllChannelsAsync();
        }
    }

    // ========== 通道信息 ==========

    public class ChannelInfo : PropertyChangedBase
    {
        private double _targetVoltage;
        private double _targetCurrent = 1.0;
        private double _actualVoltage;
        private double _actualCurrent;
        private bool _isEnabled;
        private bool _isBusy;

        public ChannelInfo(int number)
        {
            Number = number;
        }

        public int Number { get; }

        public double TargetVoltage
        {
            get => _targetVoltage;
            set
            {
                if (Math.Abs(_targetVoltage - value) < 0.001) return;
                _targetVoltage = value;
                NotifyOfPropertyChange();
            }
        }

        public double TargetCurrent
        {
            get => _targetCurrent;
            set
            {
                if (Math.Abs(_targetCurrent - value) < 0.001) return;
                _targetCurrent = value;
                NotifyOfPropertyChange();
            }
        }

        public double ActualVoltage
        {
            get => _actualVoltage;
            set
            {
                if (Math.Abs(_actualVoltage - value) < 0.001) return;
                _actualVoltage = value;
                NotifyOfPropertyChange();
            }
        }

        public double ActualCurrent
        {
            get => _actualCurrent;
            set
            {
                if (Math.Abs(_actualCurrent - value) < 0.001) return;
                _actualCurrent = value;
                NotifyOfPropertyChange();
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
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
