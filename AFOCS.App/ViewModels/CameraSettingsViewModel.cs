using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using AFOCS.App.Services;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Modules.Settings;
using AFOCS.Infrastructure;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels
{
    // ========== 相机设置基类 ==========

    public abstract class CameraSettingsViewModel : PropertyChangedBase, ISettingsEditor
    {
        private readonly IConfigService _configService;
        private readonly ICamera _camera;
        private readonly Type _configType;
        private readonly IToastService _toastService;
        private HkCameraConfig _config = new();

        private string _serialNumber = string.Empty;
        private ObservableCollection<(string, string)> _availableCameras = [];
        private bool _isScanning;
        private string _statusMessage = string.Empty;
        private bool _isBusy;
        private uint _width;
        private uint _height;
        private bool _isGrabbing;

        protected CameraSettingsViewModel(
            string name,
            IConfigService configService,
            ICamera camera,
            Type configType,
            IToastService toastService)
        {
            Name = name;
            _configService = configService;
            _camera = camera;
            _configType = configType;
            _toastService = toastService;

            _ = LoadConfigAsync();
        }

        public string Name { get; }

        string ISettingsEditor.SettingsPageName => Name;

        string ISettingsEditor.SettingsPagePath => "设备配置\\相机";

        // ========== 配置 ==========

        public string SerialNumber
        {
            get => _serialNumber;
            set
            {
                if (_serialNumber == value) return;
                _serialNumber = value;
                NotifyOfPropertyChange(() => SerialNumber);
            }
        }

        // ========== 连接状态 ==========

        public bool IsConnected => _camera.IsConnected;

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

        // ========== 图像参数 ==========

        public uint Width
        {
            get => _width;
            set { if (_width == value) return; _width = value; NotifyOfPropertyChange(); }
        }

        public uint Height
        {
            get => _height;
            set { if (_height == value) return; _height = value; NotifyOfPropertyChange(); }
        }

        public bool IsGrabbing
        {
            get => _isGrabbing;
            set { if (_isGrabbing == value) return; _isGrabbing = value; NotifyOfPropertyChange(); }
        }

        // ========== 扫描 ==========

        public ObservableCollection<(string,string)> AvailableCameras
        {
            get => _availableCameras;
            set { _availableCameras = value; NotifyOfPropertyChange(); }
        }

        public bool IsScanning
        {
            get => _isScanning;
            set { if (_isScanning == value) return; _isScanning = value; NotifyOfPropertyChange(); }
        }

        public async Task ScanAvailableCameras()
        {
            IsScanning = true;
            try
            {
                var cameras = await Task.Run(() => Camera<HkCameraConfig>.GetAllCameraSerialNumbers(Serilog.Log.Logger));
                AvailableCameras.Clear();
                foreach (var item in cameras)
                {
                    AvailableCameras.Add((item.Item1,item.Item2));
                }
  
            }
            finally { IsScanning = false; }
        }

        public void SelectCamera(System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is (string sn, string _))
                SerialNumber = sn;
        }

        // ========== 操作 ==========

        public async Task ReconnectAsync()
        {
            IsBusy = true;
            StatusMessage = "正在重连...";
            try
            {
                SaveConfig();
                var result = await _camera.ReConnectAsync();
                StatusMessage = result.IsSuccess ? "重连成功" : $"重连失败: {result.Message}";
                if (result.IsSuccess)
                {
                    Width = _camera.Width;
                    Height = _camera.Height;
                }
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
                var result = await _camera.StopAsync();
                StatusMessage = result.IsSuccess ? "已断开" : $"断开失败: {result.Message}";
            }
            catch (Exception ex) { StatusMessage = $"断开异常: {ex.Message}"; }
            finally
            {
                IsBusy = false;
                NotifyOfPropertyChange(() => IsConnected);
            }
        }

        public async Task StartGrabbingAsync()
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            IsBusy = true;
            try
            {
                var result = await _camera.StartCameraAsync();
                if (result.IsSuccess) IsGrabbing = true;
                StatusMessage = result.IsSuccess ? "采集已启动" : $"启动失败: {result.Message}";
            }
            catch (Exception ex) { StatusMessage = $"启动异常: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        public async Task StopGrabbingAsync()
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            IsBusy = true;
            try
            {
                var result = await _camera.StopCameraAsync();
                if (result.IsSuccess) IsGrabbing = false;
                StatusMessage = result.IsSuccess ? "采集已停止" : $"停止失败: {result.Message}";
            }
            catch (Exception ex) { StatusMessage = $"停止异常: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        public async Task SoftwareTriggerAsync()
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接，请先连接设备。"); return; }
            IsBusy = true;
            try
            {
                var result = await _camera.SoftwareTriggerOnce();
                StatusMessage = result.IsSuccess ? "触发成功" : $"触发失败: {result.Message}";
            }
            catch (Exception ex) { StatusMessage = $"触发异常: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        // ========== 持久化 ==========

        void ISettingsEditor.ApplyChanges()
        {
            SaveConfig();
        }

        private async Task LoadConfigAsync()
        {
            var loaded = await _configService.LoadAsync(_configType);
            _config = (loaded as HkCameraConfig) ?? new HkCameraConfig();
            _serialNumber = _config.ChSerialNumber;

            NotifyOfPropertyChange(() => SerialNumber);
            RefreshConnectionStatus();

            Width = _camera.Width;
            Height = _camera.Height;
        }

        private void SaveConfig()
        {
            _config.ChSerialNumber = _serialNumber;
            Task.Run(async () => await _configService.SaveAsync(_configType, _config));
        }
    }

    // ========== 四个相机子类 ==========

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CameraLeftUpSettingsViewModel : CameraSettingsViewModel
    {
        [ImportingConstructor]
        public CameraLeftUpSettingsViewModel(IConfigService configService, CameraLeftUp camera, IToastService toastService)
            : base("左上", configService, camera, typeof(CameraConfigLeftUp), toastService) { }
    }

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CameraLeftDownSettingsViewModel : CameraSettingsViewModel
    {
        [ImportingConstructor]
        public CameraLeftDownSettingsViewModel(IConfigService configService, CameraLeftDown camera, IToastService toastService)
            : base("左下", configService, camera, typeof(CameraConfigLeftDown), toastService) { }
    }

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CameraRightUpSettingsViewModel : CameraSettingsViewModel
    {
        [ImportingConstructor]
        public CameraRightUpSettingsViewModel(IConfigService configService, CameraRightUp camera, IToastService toastService)
            : base("右上", configService, camera, typeof(CameraConfigRightUp), toastService) { }
    }

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CameraRightDownSettingsViewModel : CameraSettingsViewModel
    {
        [ImportingConstructor]
        public CameraRightDownSettingsViewModel(IConfigService configService, CameraRightDown camera, IToastService toastService)
            : base("右下", configService, camera, typeof(CameraConfigRightDown), toastService) { }
    }
}
