using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
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
    public class HeightGaugeSettingsViewModel : Screen, ISettingsEditor
    {
        private readonly IHeightGauge _heightGauge;
        private readonly IToastService _toastService;
        private readonly HeightGaugeConfig _config = new();
        private bool _isModify;

        private readonly string[] _modifyProperties =
        [
            nameof(Ip), nameof(Port), nameof(TimeoutMs),
        ];

        [ImportingConstructor]
        public HeightGaugeSettingsViewModel(IHeightGauge heightGauge, IToastService toastService)
        {
            _heightGauge = heightGauge;
            _toastService = toastService;

            Channels = new ObservableCollection<HeightChannelInfo>
            {
                new(1), new(2), new(3), new(4),
            };

            var config = _heightGauge.GetConfig();
            _config.Ip = config.Ip;
            _config.Port = config.Port;
            _config.TimeoutMs = config.TimeoutMs;
        }

        public string SettingsPageName => "测高仪";

        public string SettingsPagePath => "设备配置";

        // ========== 配置属性 ==========

        public string Ip
        {
            get => _config.Ip;
            set
            {
                if (_config.Ip == value) return;
                _config.Ip = value;
                NotifyOfPropertyChange();
            }
        }

        public int Port
        {
            get => _config.Port;
            set
            {
                if (_config.Port == value) return;
                _config.Port = value;
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

        public bool IsConnected => _heightGauge.IsConnected;

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

        // ========== 操作 ==========

        public async Task ReconnectAsync()
        {
            IsBusy = true;
            StatusMessage = "正在重连...";
            try
            {
                if (_isModify)
                {
                    await _heightGauge.SaveConfigAsync(_config);
                    _isModify = false;
                }
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

        public async Task SaveAsync()
        {
            IsBusy = true;
            StatusMessage = "正在保存...";
            try
            {
                await _heightGauge.SaveConfigAsync(_config);
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
            if(!_isModify) return;
            _ = SaveAsync();
        }
    }

    // ========== 通道信息 ==========

    public class HeightChannelInfo : PropertyChangedBase
    {
        public HeightChannelInfo(int number)
        {
            Number = number;
        }

        public int Number { get; }

        public double Height
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
