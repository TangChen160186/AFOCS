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
    // ========== 相机设置基类 ==========

    public abstract class CameraSettingsViewModel : Screen, ISettingsEditor
    {
        private readonly ICamera _camera;
        private readonly IToastService _toastService;
        private readonly HkCameraConfig _config = new();
        private bool _isModify;

        private readonly string[] _modifyProperties = [nameof(SerialNumber)];

        protected CameraSettingsViewModel(string name, ICamera camera, IToastService toastService)
        {
            Name = name;
            _camera = camera;
            _toastService = toastService;

            var config = _camera.GetConfig();
            _config.ChSerialNumber = config.ChSerialNumber;
        }

        public string Name { get; }

        string ISettingsEditor.SettingsPageName => Name;
        string ISettingsEditor.SettingsPagePath => "设备配置\\相机";

        protected override void OnViewAttached(object view, object context)
        {
            base.OnViewAttached(view, context);
            _ = ScanAvailableCameras();
        }

        // ========== 配置 ==========

        public string SerialNumber
        {
            get => _config.ChSerialNumber;
            set
            {
                if (_config.ChSerialNumber == value) return;
                _config.ChSerialNumber = value;
                NotifyOfPropertyChange();
            }
        }

        // ========== 连接状态 ==========

        public bool IsConnected => _camera.IsConnected;

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

        // ========== 图像参数 ==========

        public uint Width
        {
            get;
            set => Set(ref field, value);
        }

        public uint Height
        {
            get;
            set => Set(ref field, value);
        }

        public bool IsGrabbing
        {
            get;
            set => Set(ref field, value);
        }

        // ========== 扫描 ==========

        public ObservableCollection<(string, string)> AvailableCameras
        {
            get;
            set => Set(ref field, value);
        } = [];

        public bool IsScanning
        {
            get;
            set => Set(ref field, value);
        }

        public async Task ScanAvailableCameras()
        {
            IsScanning = true;
            try
            {
                var cameras = await Task.Run(() =>
                    Camera<HkCameraConfig>.GetAllCameraSerialNumbers(Serilog.Log.Logger));
                AvailableCameras = new ObservableCollection<(string, string)>();
                foreach (var item in cameras)
                    AvailableCameras.Add(item);
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
                if (_isModify)
                {
                    await _camera.SaveConfigAsync(_config);
                    _isModify = false;
                }
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

        public async Task SaveAsync()
        {
            IsBusy = true;
            StatusMessage = "正在保存...";
            try
            {
                await _camera.SaveConfigAsync(_config);
                _isModify = false;
                StatusMessage = "配置已保存";
            }
            catch (Exception ex) { StatusMessage = $"保存异常: {ex.Message}"; }
            finally { IsBusy = false; }
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

        // ========== ISettingsEditor ==========

        public override void NotifyOfPropertyChange([CallerMemberName] string? propertyName = null)
        {
            base.NotifyOfPropertyChange(propertyName);

            if (_modifyProperties.Contains(propertyName))
                _isModify = true;
        }

        public void ApplyChanges()
        {
            if (!_isModify) return;
            _ = SaveAsync();
        }
    }

    // ========== 四个相机子类 ==========

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CameraLeftUpSettingsViewModel : CameraSettingsViewModel
    {
        [ImportingConstructor]
        public CameraLeftUpSettingsViewModel(CameraLeftUp camera, IToastService toastService)
            : base("左上", camera, toastService) { }
    }

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CameraLeftDownSettingsViewModel : CameraSettingsViewModel
    {
        [ImportingConstructor]
        public CameraLeftDownSettingsViewModel(CameraLeftDown camera, IToastService toastService)
            : base("左下", camera, toastService) { }
    }

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CameraRightUpSettingsViewModel : CameraSettingsViewModel
    {
        [ImportingConstructor]
        public CameraRightUpSettingsViewModel(CameraRightUp camera, IToastService toastService)
            : base("右上", camera, toastService) { }
    }

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CameraRightDownSettingsViewModel : CameraSettingsViewModel
    {
        [ImportingConstructor]
        public CameraRightDownSettingsViewModel(CameraRightDown camera, IToastService toastService)
            : base("右下", camera, toastService) { }
    }
}
