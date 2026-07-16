using System.ComponentModel.Composition;
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
    public class OpticalPowerMeterSettingsViewModel : PropertyChangedBase, ISettingsEditor
    {
        private readonly IConfigService _configService;

        [ImportingConstructor]
        public OpticalPowerMeterSettingsViewModel(
            IConfigService configService,
            OpticalPowerMeterLeft meterLeft,
            OpticalPowerMeterRight meterRight,
            IToastService toastService)
        {
            _configService = configService;

            Left = new OpmSideInfo("左工位", meterLeft, typeof(OpticalPowerMeterConfigLeft), configService, toastService);
            Right = new OpmSideInfo("右工位", meterRight, typeof(OpticalPowerMeterConfigRight), configService, toastService);

            _ = LoadConfigAsync();
        }

        public string SettingsPageName => "光功率计";

        public string SettingsPagePath => "设备配置";

        public OpmSideInfo Left { get; }
        public OpmSideInfo Right { get; }

        public void ApplyChanges()
        {
            Left.SaveConfig();
            Right.SaveConfig();
        }

        private async Task LoadConfigAsync()
        {
            await Task.WhenAll(Left.LoadConfigAsync(), Right.LoadConfigAsync());
        }
    }

    // ========== 工位信息 ==========

    public class OpmSideInfo : PropertyChangedBase
    {
        private readonly IOpticalPowerMeter _meter;
        private readonly Type _configType;
        private readonly IConfigService _cfgService;
        private readonly IToastService _toastService;
        private OpticalPowerMeterConfig _config = new();

        private string _ip = string.Empty;
        private int _port;
        private bool _isBusy;
        private string _statusMessage = string.Empty;
        private int _slot = 1;
        private int _channel = 1;

        // OS
        private double _osPowerRead;
        private double _osPowerSet;
        private string _osStatus = "";

        // OPM
        private double _opmPowerRead;
        private double _opmOffsetRead;
        private double _opmOffsetSet;

        public OpmSideInfo(string name, IOpticalPowerMeter meter, Type configType, IConfigService cfgService, IToastService toastService)
        {
            Name = name;
            _meter = meter;
            _configType = configType;
            _cfgService = cfgService;
            _toastService = toastService;
        }

        public string Name { get; }

        // ========== 连接 ==========

        public bool IsConnected => _meter.IsConnected;

        public string StatusMessage
        {
            get => _statusMessage;
            set { if (_statusMessage == value) return; _statusMessage = value; NotifyOfPropertyChange(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { if (_isBusy == value) return; _isBusy = value; NotifyOfPropertyChange(); }
        }

        public void RefreshConnectionStatus()
        {
            NotifyOfPropertyChange(() => IsConnected);
            StatusMessage = IsConnected ? "已连接" : "未连接";
        }

        // ========== 配置 ==========

        public string Ip
        {
            get => _ip;
            set { if (_ip == value) return; _ip = value; NotifyOfPropertyChange(); }
        }

        public int Port
        {
            get => _port;
            set { if (_port == value) return; _port = value; NotifyOfPropertyChange(); }
        }

        // ========== Slot/Channel ==========

        public int Slot
        {
            get => _slot;
            set { if (_slot == value) return; _slot = value; NotifyOfPropertyChange(); }
        }

        public int Channel
        {
            get => _channel;
            set { if (_channel == value) return; _channel = value; NotifyOfPropertyChange(); }
        }

        // ========== OS 光源 ==========

        public double OsPowerRead
        {
            get => _osPowerRead;
            set { if (Math.Abs(_osPowerRead - value) < 0.0001) return; _osPowerRead = value; NotifyOfPropertyChange(); }
        }

        public double OsPowerSet
        {
            get => _osPowerSet;
            set { if (Math.Abs(_osPowerSet - value) < 0.0001) return; _osPowerSet = value; NotifyOfPropertyChange(); }
        }

        public string OsStatus
        {
            get => _osStatus;
            set { if (_osStatus == value) return; _osStatus = value; NotifyOfPropertyChange(); }
        }

        // ========== OPM 功率计 ==========

        public double OpmPowerRead
        {
            get => _opmPowerRead;
            set { if (Math.Abs(_opmPowerRead - value) < 0.0001) return; _opmPowerRead = value; NotifyOfPropertyChange(); }
        }

        public double OpmOffsetRead
        {
            get => _opmOffsetRead;
            set { if (Math.Abs(_opmOffsetRead - value) < 0.0001) return; _opmOffsetRead = value; NotifyOfPropertyChange(); }
        }

        public double OpmOffsetSet
        {
            get => _opmOffsetSet;
            set { if (Math.Abs(_opmOffsetSet - value) < 0.0001) return; _opmOffsetSet = value; NotifyOfPropertyChange(); }
        }

        // ========== 操作 ==========

        public async Task ReconnectAsync()
        {
            IsBusy = true;
            StatusMessage = "正在重连...";
            try
            {
                SaveConfig();
                var result = await _meter.ReConnectAsync();
                StatusMessage = result.IsSuccess ? "重连成功" : $"重连失败: {result.Message}";
            }
            catch (Exception ex) { StatusMessage = $"重连异常: {ex.Message}"; }
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
                var result = await _meter.StopAsync();
                StatusMessage = result.IsSuccess ? "已断开" : $"断开失败: {result.Message}";
            }
            catch (Exception ex) { StatusMessage = $"断开异常: {ex.Message}"; }
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
                var pResult = await _meter.GetOsPowerAsync(_slot, _channel);
                if (pResult.IsSuccess) OsPowerRead = pResult.Data;

                var sResult = await _meter.GetOsStatusAsync(_slot, _channel);
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
                await _meter.SetOsPowerAsync(_slot, _channel, _osPowerSet);
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
                var pResult = await _meter.GetOpmPowerAsync(_slot, _channel);
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
                var oResult = await _meter.GetOpmOffsetAsync(_slot, _channel);
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
                await _meter.SetOpmOffsetAsync(_slot, _channel, _opmOffsetSet);
                await ReadOpmOffsetAsync();
            }
            finally { IsBusy = false; }
        }

        // ========== 持久化 ==========

        public async Task LoadConfigAsync()
        {
            var loaded = await _cfgService.LoadAsync(_configType);
            _config = (loaded as OpticalPowerMeterConfig) ?? new OpticalPowerMeterConfig();
            _ip = _config.Ip;
            _port = _config.Port;

            NotifyOfPropertyChange(() => Ip);
            NotifyOfPropertyChange(() => Port);
            RefreshConnectionStatus();
        }

        public void SaveConfig()
        {
            _config.Ip = _ip;
            _config.Port = _port;
            Task.Run(async () => await _cfgService.SaveAsync(_configType, _config));
        }
    }
}
