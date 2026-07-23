using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Windows;
using AFOCS.App.Services;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Modules.Settings;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class PowerSupplySettingsViewModel : Screen, ISettingsEditor
    {
        private readonly IProgrammablePowerSupply _powerSupply;
        private readonly IToastService _toastService;
        private ProgrammablePowerSupplyConfig _editConfig = new();

        private ObservableCollection<string> _availableResources = [];
        private bool _isScanning;
        private bool _isBusy;
        private string _statusMessage = string.Empty;

        [ImportingConstructor]
        public PowerSupplySettingsViewModel(IProgrammablePowerSupply powerSupply, IToastService toastService)
        {
            _powerSupply = powerSupply;
            _toastService = toastService;

            Channels = new ObservableCollection<ChannelInfo>
            {
                new(1), new(2)
            };

            var config = _powerSupply.GetConfig();
            _editConfig.VisaAddress = config.VisaAddress;
            _editConfig.TimeoutMs = config.TimeoutMs;
        }

        public string SettingsPageName => "可编程电源";

        public string SettingsPagePath => "设备配置";

        // ========== 生命周期 ==========

        protected override void OnViewAttached(object view, object context)
        {
            base.OnViewAttached(view, context);
            _ = ScanAndRefreshAsync();

            if (view is FrameworkElement fe)
                fe.Unloaded += OnViewUnloaded;
        }

        private void OnViewUnloaded(object? sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
                fe.Unloaded -= OnViewUnloaded;
        }

        private async Task ScanAndRefreshAsync()
        {
            await ScanAvailableResources();
        }

        // ========== 配置属性 ==========

        public string VisaAddress
        {
            get => _editConfig.VisaAddress;
            set
            {
                if (_editConfig.VisaAddress == value) return;
                _editConfig.VisaAddress = value;
                NotifyOfPropertyChange();
            }
        }

        public int TimeoutMs
        {
            get => _editConfig.TimeoutMs;
            set
            {
                if (_editConfig.TimeoutMs == value) return;
                _editConfig.TimeoutMs = value;
                NotifyOfPropertyChange();
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
                NotifyOfPropertyChange();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                NotifyOfPropertyChange();
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
                await _powerSupply.SaveConfigAsync(_editConfig);
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
                NotifyOfPropertyChange();
            }
        }

        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (_isScanning == value) return;
                _isScanning = value;
                NotifyOfPropertyChange();
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
            if (!_powerSupply.IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
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
            if (!_powerSupply.IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
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
            if (!_powerSupply.IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
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
            if (!_powerSupply.IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            foreach (var ch in Channels)
                await ReadChannelAsync(ch);
        }

        // ========== ISettingsEditor ==========

        public void ApplyChanges()
        {
            _ = ReconnectAsync();
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
