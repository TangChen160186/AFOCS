using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using AFOCS.App.Services;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Modules.Settings;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels.Settings
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class OpticalSwitchSettingsViewModel : Screen, ISettingsEditor
    {
        private readonly IOpticalSwitch _opticalSwitch;
        private readonly IToastService _toastService;
        private readonly OpticalSwitchConfig _config = new();
        private bool _isModify;

        private readonly string[] _modifyProperties =
        [
            nameof(Ip), nameof(Port), nameof(TimeoutMs),
        ];

        [ImportingConstructor]
        public OpticalSwitchSettingsViewModel(IOpticalSwitch opticalSwitch, IToastService toastService)
        {
            _opticalSwitch = opticalSwitch;
            _toastService = toastService;

            Groups = new ObservableCollection<GroupInfo>();
            for (int i = 1; i <= 16; i++)
                Groups.Add(new GroupInfo(i));

            var config = _opticalSwitch.GetConfig();
            _config.Ip = config.Ip;
            _config.Port = config.Port;
            _config.TimeoutMs = config.TimeoutMs;
        }

        public string SettingsPageName => "光开关";

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

        public bool IsConnected => _opticalSwitch.IsConnected;

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
                    await _opticalSwitch.SaveConfigAsync(_config);
                    _isModify = false;
                }
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

        public async Task SaveAsync()
        {
            IsBusy = true;
            StatusMessage = "正在保存...";
            try
            {
                await _opticalSwitch.SaveConfigAsync(_config);
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

        // ========== 通道分组 ==========

        public ObservableCollection<GroupInfo> Groups { get; }

        public string SN
        {
            get;
            set => Set(ref field, value);
        }

        public string PN
        {
            get;
            set => Set(ref field, value);
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

    // ========== 通道组信息 ==========

    public class GroupInfo : PropertyChangedBase
    {
        public GroupInfo(int number)
        {
            Number = number;
        }

        public int Number { get; }

        public int CurrentChannel
        {
            get;
            set => Set(ref field, value);
        }

        public int TargetChannel
        {
            get;
            set => Set(ref field, value);
        } = 1;

        public bool IsBusy
        {
            get;
            set => Set(ref field, value);
        }
    }
}
