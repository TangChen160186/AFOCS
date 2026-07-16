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
    public class OpticalSwitchSettingsViewModel : PropertyChangedBase, ISettingsEditor
    {
        private readonly IConfigService _configService;
        private readonly OpticalSwitch _opticalSwitch;
        private readonly IToastService _toastService;
        private OpticalSwitchConfig _config = new();

        private string _ip = string.Empty;
        private int _port;
        private bool _isBusy;
        private string _statusMessage = string.Empty;
        private string _sn = string.Empty;
        private string _pn = string.Empty;

        [ImportingConstructor]
        public OpticalSwitchSettingsViewModel(IConfigService configService, OpticalSwitch opticalSwitch, IToastService toastService)
        {
            _configService = configService;
            _opticalSwitch = opticalSwitch;
            _toastService = toastService;

            Groups = new ObservableCollection<GroupInfo>();
            for (int i = 1; i <= 16; i++)
                Groups.Add(new GroupInfo(i));

            _ = LoadConfigAsync();
        }

        public string SettingsPageName => "光开关";

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

        public bool IsConnected => _opticalSwitch.IsConnected;

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

                var result = await _opticalSwitch.ReConnectAsync();
                StatusMessage = result.IsSuccess ? "重连成功" : $"重连失败: {result.Message}";
                if (result.IsSuccess)
                    _ = ReadAllAsync();
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
                var result = await _opticalSwitch.StopAsync();
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

        // ========== 通道分组 ==========

        public ObservableCollection<GroupInfo> Groups { get; }

        public string SN
        {
            get => _sn;
            set
            {
                if (_sn == value) return;
                _sn = value;
                NotifyOfPropertyChange(() => SN);
            }
        }

        public string PN
        {
            get => _pn;
            set
            {
                if (_pn == value) return;
                _pn = value;
                NotifyOfPropertyChange(() => PN);
            }
        }

        public async Task ReadAllAsync()
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }

            var sResult = await _opticalSwitch.GetAllChannelStatusAsync();
            if (sResult.IsSuccess && sResult.Data != null)
            {
                foreach (var kv in sResult.Data)
                {
                    var group = Groups.FirstOrDefault(g => g.Number == kv.Key);
                    if (group != null)
                        group.CurrentChannel = kv.Value;
                }
            }

            var snResult = await _opticalSwitch.GetSnAsync();
            if (snResult.IsSuccess)
                SN = snResult.Data ?? "";

            var pnResult = await _opticalSwitch.GetPnAsync();
            if (pnResult.IsSuccess)
                PN = pnResult.Data ?? "";
        }

        public async Task SwitchGroupAsync(GroupInfo group)
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            group.IsBusy = true;
            try
            {
                var result = await _opticalSwitch.SwitchChannelAsync(group.Number, group.TargetChannel);
                if (result.IsSuccess && result.Data)
                    group.CurrentChannel = group.TargetChannel;
            }
            finally
            {
                group.IsBusy = false;
            }
        }

        public async Task ReadGroupAsync(GroupInfo group)
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            group.IsBusy = true;
            try
            {
                var result = await _opticalSwitch.GetAllChannelStatusAsync();
                if (result.IsSuccess && result.Data != null
                    && result.Data.TryGetValue(group.Number, out var ch))
                {
                    group.CurrentChannel = ch;
                }
            }
            finally
            {
                group.IsBusy = false;
            }
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
            _config = await _configService.LoadAsync<OpticalSwitchConfig>()
                      ?? new OpticalSwitchConfig();
            _ip = _config.Ip;
            _port = _config.Port;

            NotifyOfPropertyChange(() => Ip);
            NotifyOfPropertyChange(() => Port);
            RefreshConnectionStatus();
            _ = ReadAllAsync();
        }
    }

    // ========== 通道组信息 ==========

    public class GroupInfo : PropertyChangedBase
    {
        private int _currentChannel;
        private int _targetChannel = 1;
        private bool _isBusy;

        public GroupInfo(int number)
        {
            Number = number;
        }

        public int Number { get; }

        public int CurrentChannel
        {
            get => _currentChannel;
            set
            {
                if (_currentChannel == value) return;
                _currentChannel = value;
                NotifyOfPropertyChange();
            }
        }

        public int TargetChannel
        {
            get => _targetChannel;
            set
            {
                if (_targetChannel == value) return;
                _targetChannel = value;
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
