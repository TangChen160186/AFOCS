using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AFOCS.App.Services;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Modules.Settings;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels.Settings
{
    // ========== 相机设置基类 ==========

    public abstract class CameraSettingsViewModel : Screen, ISettingsEditor
    {
        private readonly ICamera _camera;
        private readonly IToastService _toastService;
        private readonly HkCameraConfig _config = new();
        private bool _isModify;
        private WriteableBitmap? _previewBitmap;

        private readonly string[] _modifyProperties = [nameof(SerialNumber), nameof(Precision)];

        protected CameraSettingsViewModel(string name, ICamera camera, IToastService toastService)
        {
            Name = name;
            _camera = camera;
            _toastService = toastService;

            var config = _camera.GetConfig();
            _config.ChSerialNumber = config.ChSerialNumber;
            _config.Precision = config.Precision;
        }

        public string Name { get; }

        string ISettingsEditor.SettingsPageName => Name;
        string ISettingsEditor.SettingsPagePath => "设备配置\\相机";

        // ========== 生命周期 ==========

        protected override void OnViewAttached(object view, object context)
        {
            base.OnViewAttached(view, context);
            Subscribe();
            _ = ScanAvailableCameras();

            if (view is FrameworkElement fe)
                fe.Unloaded += OnViewUnloaded;
        }

        private void OnViewUnloaded(object? sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
                fe.Unloaded -= OnViewUnloaded;
            Unsubscribe();
        }

        private void Subscribe()
        {
            _camera.ImageReceived += OnImageReceived;
        }

        private void Unsubscribe()
        {
            _camera.ImageReceived -= OnImageReceived;
        }

        // ========== 图像预览 ==========

        public BitmapSource? PreviewImage
        {
            get;
            set => Set(ref field, value);
        }

        private void OnImageReceived(object? sender, ImagePreviewedEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                int w = e.Width, h = e.Height;
                Width = (uint)e.Width;
                Height = (uint)e.Height;
                int bytesPerPixel = e.IsMono ? 1 : 3;
                var format = e.IsMono ? PixelFormats.Gray8 : PixelFormats.Bgr24;

                if (_previewBitmap == null || _previewBitmap.PixelWidth != w || _previewBitmap.PixelHeight != h)
                    _previewBitmap = new WriteableBitmap(w, h, 96, 96, format, null);

                int stride = w * bytesPerPixel;
                _previewBitmap.WritePixels(new Int32Rect(0, 0, w, h), e.ImageData, stride * h, stride);
                PreviewImage = _previewBitmap;
            });
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

        public double Precision
        {
            get => _config.Precision;
            set
            {
                if (Math.Abs(_config.Precision - value) < 1e-10) return;
                _config.Precision = value;
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
            Unsubscribe();
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
                    Subscribe();
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

        public async Task CaptureAsync()
        {
            if (!IsConnected) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "保存图像",
                Filter = "BMP 图像|*.bmp",
                DefaultExt = ".bmp",
                FileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            };

            if (dlg.ShowDialog() != true) return;

            IsBusy = true;
            StatusMessage = "正在抓图...";
            try
            {
                var result = await _camera.CaptureImageAsync(dlg.FileName);
                StatusMessage = result.IsSuccess
                    ? $"已保存: {Path.GetFileName(result.Data)}"
                    : $"抓图失败: {result.Message}";
            }
            catch (Exception ex) { StatusMessage = $"抓图异常: {ex.Message}"; }
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
    [method: ImportingConstructor]
    public class CameraLeftUpSettingsViewModel(CameraLeftUp camera, IToastService toastService)
        : CameraSettingsViewModel("左上", camera, toastService);

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class CameraLeftDownSettingsViewModel(CameraLeftDown camera, IToastService toastService)
        : CameraSettingsViewModel("左下", camera, toastService);

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class CameraRightUpSettingsViewModel(CameraRightUp camera, IToastService toastService)
        : CameraSettingsViewModel("右上", camera, toastService);

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class CameraRightDownSettingsViewModel(CameraRightDown camera, IToastService toastService)
        : CameraSettingsViewModel("右下", camera, toastService);
}
