using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using AFOCS.App.Services;
using AFOCS.Devices.OpticalPowerMeters;
using AFOCS.Framework.Modules.Settings;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels.Settings
{
    /// <summary>
    /// 光功率计设置基类 —— 每个子类对应一个工位
    /// </summary>
    public abstract class OpticalPowerMeterSettingsViewModel : Screen, ISettingsEditor
    {
        protected readonly IOpticalPowerMeter Meter;
        private readonly IToastService _toastService;
        private OpticalPowerMeterConfig _config = new();
        private bool _isModify = false;

        private readonly string[] _modifyProperties =
        [
            nameof(Ip), nameof(Port), nameof(TimeoutMs),
        ];

        string ISettingsEditor.SettingsPageName => Name;
        string ISettingsEditor.SettingsPagePath => "设备配置\\光功率计";

        protected abstract string Name { get; }

        protected OpticalPowerMeterSettingsViewModel(
            IOpticalPowerMeter meter,
            IToastService toastService)
        {
            Meter = meter;
            _toastService = toastService;

            var config = Meter.GetConfig();
            _config.Ip = config.Ip;
            _config.Port = config.Port;
            _config.TimeoutMs = config.TimeoutMs;
        }

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

        public bool IsConnected => Meter.IsConnected;

        public string StatusMessage
        {
            get;
            set => Set(ref field, value);
        } = string.Empty;

        public bool IsBusy
        {
            get;
            set => Set(ref field, value);
        }

        public void RefreshConnectionStatus()
        {
            NotifyOfPropertyChange(() => IsConnected);
            StatusMessage = IsConnected ? "已连接" : "未连接";
        }

        // ========== Slot/Channel ==========


        public int Slot
        {
            get => field;
            set => Set(ref field, value);
        } = 1;


        public int Channel
        {
            get => field;
            set => Set(ref field, value);
        } = 1;

        // ========== OS 光源 ==========

        public double OsPowerRead
        {
            get => field;
            set => Set(ref field, value);
        }

        public double OsPowerSet
        {
            get => field;
            set => Set(ref field, value);
        }

        public string OsStatus
        {
            get => field;
            set => Set(ref field, value);
        }

        // ========== OPM 功率计 ==========

        public double OpmPowerRead
        {
            get => field;
            set => Set(ref field, value);
        }


        public double OpmOffsetRead
        {
            get => field;
            set => Set(ref field, value);
        }


        public double OpmOffsetSet
        {
            get => field;
            set => Set(ref field, value);
        }

        // ========== NotifyOfPropertyChange 重写 ==========

        public override void NotifyOfPropertyChange([CallerMemberName] string? propertyName = null)
        {
            base.NotifyOfPropertyChange(propertyName);

            if (_modifyProperties.Contains(propertyName))
            {
                _isModify = true;
            }
        }

        // ========== 操作 ==========

        public async Task SaveAsync()
        {
            IsBusy = true;
            StatusMessage = "正在保存...";
            try
            {
                await Meter.SaveConfigAsync(_config);
                _isModify = false;
                StatusMessage = "配置已保存";
            }
            catch (Exception ex) { StatusMessage = $"保存异常: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        public async Task ReconnectAsync()
        {
            IsBusy = true;
            StatusMessage = "正在重连...";
            try
            {
                if (_isModify)
                {
                    await Meter.SaveConfigAsync(_config);
                    _isModify = false;
                }
                var result = await Meter.ReConnectAsync();
                StatusMessage = result.IsSuccess ? "重连成功" : $"重连失败: {result.Message}";
            }
            catch (Exception ex) { StatusMessage = $"重连异常: {ex.Message}"; }
            finally
            {
                IsBusy = false;
                NotifyOfPropertyChange(() => IsConnected);
            }
        }

        // ========== OS 光源操作 ==========

        public async Task ReadOsAsync()
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            IsBusy = true;
            try
            {
                var pResult = await Meter.GetOsPowerAsync(Slot, Channel);
                if (pResult.IsSuccess) OsPowerRead = pResult.Data;

                var sResult = await Meter.GetOsStatusAsync(Slot, Channel);
                if (sResult.IsSuccess) OsStatus = sResult.Data ? "ON" : "OFF";
            }
            finally { IsBusy = false; }
        }

        public async Task SetOsPowerAsync()
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            IsBusy = true;
            try
            {
                await Meter.SetOsPowerAsync(Slot, Channel, OsPowerSet);
                await ReadOsAsync();
            }
            finally { IsBusy = false; }
        }

        // ========== OPM 功率计操作 ==========

        public async Task ReadOpmPowerAsync()
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            IsBusy = true;
            try
            {
                var pResult = await Meter.GetOpmPowerAsync(Slot, Channel);
                if (pResult.IsSuccess) OpmPowerRead = pResult.Data;
            }
            finally { IsBusy = false; }
        }

        public async Task ReadOpmOffsetAsync()
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            IsBusy = true;
            try
            {
                var oResult = await Meter.GetOpmOffsetAsync(Slot, Channel);
                if (oResult.IsSuccess) OpmOffsetRead = oResult.Data;
            }
            finally { IsBusy = false; }
        }

        public async Task SetOpmOffsetAsync()
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            IsBusy = true;
            try
            {
                await Meter.SetOpmOffsetAsync(Slot, Channel, OpmOffsetSet);
                await ReadOpmOffsetAsync();
            }
            finally { IsBusy = false; }
        }

        // ========== ISettingsEditor ==========

        public void ApplyChanges()
        {
            if(!_isModify) 
                return;
            _ = SaveAsync();
        }
    }

    // ====================================================================
    // 两个工位子类
    // ====================================================================

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class OpticalPowerMeterLeftSettingsViewModel(
        OpticalPowerMeterLeft meter,
        IToastService toastService)
        : OpticalPowerMeterSettingsViewModel(meter, toastService)
    {
        protected override string Name => "左工位";
    }

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class OpticalPowerMeterRightSettingsViewModel(
        OpticalPowerMeterRight meter,
        IToastService toastService)
        : OpticalPowerMeterSettingsViewModel(meter, toastService)
    {
        protected override string Name => "右工位";
    }
}
