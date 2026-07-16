using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO.Ports;
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
    public class GlueDispenserSettingsViewModel : PropertyChangedBase, ISettingsEditor
    {
        private readonly IConfigService _configService;
        private readonly GlueDispenserLeft _dispenserLeft;
        private readonly GlueDispenserRight _dispenserRight;
        private readonly IToastService _toastService;

        [ImportingConstructor]
        public GlueDispenserSettingsViewModel(
            IConfigService configService,
            GlueDispenserLeft dispenserLeft,
            GlueDispenserRight dispenserRight,
            IToastService toastService)
        {
            _configService = configService;
            _dispenserLeft = dispenserLeft;
            _dispenserRight = dispenserRight;
            _toastService = toastService;

            Left = new GlueDispenserSideInfo("左工位", _dispenserLeft, typeof(GlueDispenserConfigLeft), _configService, _toastService);
            Right = new GlueDispenserSideInfo("右工位", _dispenserRight, typeof(GlueDispenserConfigRight), _configService, _toastService);

            _ = LoadConfigAsync();
        }

        public string SettingsPageName => "点胶机";

        public string SettingsPagePath => "设备配置";

        public GlueDispenserSideInfo Left { get; }
        public GlueDispenserSideInfo Right { get; }

        // ========== ISettingsEditor ==========

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

    public class GlueDispenserSideInfo : PropertyChangedBase
    {
        private readonly IGlueDispenser _dispenser;
        private readonly Type _configType;
        private readonly IConfigService _cfgService;
        private readonly IToastService _toastService;
        private GlueDispenserConfig _config = new();

        private string _portName = string.Empty;
        private int _baudRate = 9600;
        private bool _isBusy;
        private string _statusMessage = string.Empty;

        public GlueDispenserSideInfo(string name, IGlueDispenser dispenser, Type configType, IConfigService cfgService, IToastService toastService)
        {
            Name = name;
            _dispenser = dispenser;
            _configType = configType;
            _cfgService = cfgService;
            _toastService = toastService;
        }

        public string Name { get; }

        public bool IsConnected => _dispenser.IsConnected;

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

        // ========== 配置 ==========

        public string PortName
        {
            get => _portName;
            set
            {
                if (_portName == value) return;
                _portName = value;
                NotifyOfPropertyChange();
            }
        }

        public int BaudRate
        {
            get => _baudRate;
            set
            {
                if (_baudRate == value) return;
                _baudRate = value;
                NotifyOfPropertyChange();
            }
        }

        // ========== 端口扫描 ==========

        private ObservableCollection<string> _availablePorts = [];

        public ObservableCollection<string> AvailablePorts
        {
            get => _availablePorts;
            set
            {
                _availablePorts = value;
                NotifyOfPropertyChange();
            }
        }

        private bool _isScanning;

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

        public async Task ScanPortsAsync()
        {
            IsScanning = true;
            try
            {
                var ports = await Task.Run(() => SerialPort.GetPortNames());
                AvailablePorts = new ObservableCollection<string>(ports);
            }
            finally
            {
                IsScanning = false;
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
                SaveConfig();

                var result = await _dispenser.ReConnectAsync();
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
                var result = await _dispenser.StopAsync();
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

        public async Task ShotAsync()
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            IsBusy = true;
            StatusMessage = "正在点胶...";
            try
            {
                var result = await _dispenser.ShotAsync();
                StatusMessage = result.IsSuccess ? "点胶完成" : $"点胶失败: {result.Message}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"点胶异常: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ========== 持久化 ==========

        public async Task LoadConfigAsync()
        {
            var loaded = await _cfgService.LoadAsync(_configType);
            _config = (loaded as GlueDispenserConfig) ?? new GlueDispenserConfig();
            _portName = _config.PortName;
            _baudRate = _config.BaudRate;

            NotifyOfPropertyChange(() => PortName);
            NotifyOfPropertyChange(() => BaudRate);
            RefreshConnectionStatus();
        }

        public async void SaveConfig()
        {
            _config.PortName = _portName;
            _config.BaudRate = _baudRate;
            await _cfgService.SaveAsync(_configType, _config);
        }
    }
}
