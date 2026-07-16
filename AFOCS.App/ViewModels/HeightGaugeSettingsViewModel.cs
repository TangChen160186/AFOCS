using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using AFOCS.App.Services;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Modules.Settings;
using AFOCS.Infrastructure;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class HeightGaugeSettingsViewModel : PropertyChangedBase, ISettingsEditor
    {
        private readonly IConfigService _configService;
        private readonly HeightGauge _heightGauge;
        private readonly IToastService _toastService;
        private HeightGaugeConfig _config = new();

        private string _ip = string.Empty;
        private int _port;
        private bool _isBusy;
        private string _statusMessage = string.Empty;

        [ImportingConstructor]
        public HeightGaugeSettingsViewModel(IConfigService configService, HeightGauge heightGauge, IToastService toastService)
        {
            _configService = configService;
            _heightGauge = heightGauge;
            _toastService = toastService;

            Channels = new ObservableCollection<HeightChannelInfo>
            {
                new(1), new(2)
            };

            _ = LoadConfigAsync();
        }

        public string SettingsPageName => "测高仪";

        public string SettingsPagePath => "设备配置";

        // ========== 配置属性 ==========

        public string Ip
        {
            get => _ip;
            set
            {
                if (_ip == value) return;
                _ip = value;
                NotifyOfPropertyChange(() => Ip);
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                if (_port == value) return;
                _port = value;
                NotifyOfPropertyChange(() => Port);
            }
        }

        // ========== 连接状态 ==========

        public bool IsConnected => _heightGauge.IsConnected;

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
                _config.Ip = _ip;
                _config.Port = _port;
                await _configService.SaveAsync(_config);

                var result = await _heightGauge.ReConnectAsync();
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
                var result = await _heightGauge.StopAsync();
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

        // ========== 通道测量 ==========

        public ObservableCollection<HeightChannelInfo> Channels { get; }

        public async Task ReadChannelAsync(HeightChannelInfo ch)
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            ch.IsBusy = true;
            try
            {
                var result = await _heightGauge.GetHeightAsync(ch.Number);
                if (result.IsSuccess)
                    ch.Height = result.Data;
            }
            finally
            {
                ch.IsBusy = false;
            }
        }

        public async Task ReadAllChannelsAsync()
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            IsBusy = true;
            try
            {
                foreach (var ch in Channels)
                    await ReadChannelAsync(ch);
            }
            finally { IsBusy = false; }
        }

        // ========== ISettingsEditor ==========

        public void ApplyChanges()
        {
            _config.Ip = _ip;
            _config.Port = _port;
            Task.Run(async () => await _configService.SaveAsync(_config));
        }

        private async Task LoadConfigAsync()
        {
            _config = await _configService.LoadAsync<HeightGaugeConfig>()
                      ?? new HeightGaugeConfig();
            _ip = _config.Ip;
            _port = _config.Port;

            NotifyOfPropertyChange(() => Ip);
            NotifyOfPropertyChange(() => Port);
            RefreshConnectionStatus();
        }
    }

    // ========== 通道信息 ==========

    public class HeightChannelInfo : PropertyChangedBase
    {
        private double _height;
        private bool _isBusy;

        public HeightChannelInfo(int number)
        {
            Number = number;
        }

        public int Number { get; }

        public double Height
        {
            get => _height;
            set
            {
                if (Math.Abs(_height - value) < 0.0001) return;
                _height = value;
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
