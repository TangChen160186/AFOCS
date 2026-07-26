using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Windows;
using AFOCS.App.Services;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Modules.Settings;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels.Settings
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class PowerSupplySettingsViewModel : Screen, ISettingsEditor
    {
        private readonly IProgrammablePowerSupply _powerSupply;
        private readonly IToastService _toastService;
        private readonly ProgrammablePowerSupplyConfig _config = new();
        private bool _isModify;

        private readonly string[] _modifyProperties =
        [
            nameof(VisaAddress), nameof(TimeoutMs),
        ];
        [ImportingConstructor]
        public PowerSupplySettingsViewModel(IProgrammablePowerSupply powerSupply, IToastService toastService)
        {
            _powerSupply = powerSupply;
            _toastService = toastService;

            Channels = [new(1), new(2)];

            var config = _powerSupply.GetConfig();
            _config.VisaAddress = config.VisaAddress;
            _config.TimeoutMs = config.TimeoutMs;
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
            get => _config.VisaAddress;
            set
            {
                if (_config.VisaAddress == value) return;
                _config.VisaAddress = value;
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

        public bool IsConnected => _powerSupply.IsConnected;

        public bool IsBusy
        {
            get;
            set
            {
                if (field == value) return;
                field = value;
                NotifyOfPropertyChange();
            }
        }

        public string StatusMessage
        {
            get;
            set
            {
                if (field == value) return;
                field = value;
                NotifyOfPropertyChange();
            }
        } = string.Empty;

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
                if (_isModify)
                {
                    await _powerSupply.SaveConfigAsync(_config);
                    _isModify = false;
                }
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

        public async Task SaveAsync()
        {
            IsBusy = true;
            StatusMessage = "正在保存...";
            try
            {
                await _powerSupply.SaveConfigAsync(_config);
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

        // ========== 扫描 ==========

        public ObservableCollection<string> AvailableResources
        {
            get;
            set
            {
                field = value;
                NotifyOfPropertyChange();
            }
        } = [];

        public bool IsScanning
        {
            get;
            set
            {
                if (field == value) return;
                field = value;
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

        public override void NotifyOfPropertyChange([CallerMemberName]string? propertyName = null)
        {
            base.NotifyOfPropertyChange(propertyName);

            if (_modifyProperties.Contains(propertyName))
            {
                _isModify = true;
            }
        }

      
        public void ApplyChanges()
        {
            if(_isModify)
                _ = SaveAsync();
        }
    }

    // ========== 通道信息 ==========

    public class ChannelInfo(int number) : PropertyChangedBase
    {
        public int Number { get; } = number;

        public double TargetVoltage
        {
            get;
            set => Set(ref field,value);
        }

        public double TargetCurrent
        {
            get;
            set => Set(ref field, value);
        } = 1.0;

        public double ActualVoltage
        {
            get;
            set => Set(ref field, value);
        }

        public double ActualCurrent
        {
            get;
            set => Set(ref field, value);
        }

        public bool IsEnabled
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
