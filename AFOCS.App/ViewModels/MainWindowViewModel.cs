using AFOCS.App.Devices.Implementation;
using Caliburn.Micro;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AFOCS.App.ViewModels
{
    public class MainWindowViewModel: Screen
    {

    //    private BitmapSource? _previewImage;

    //    public BitmapSource? PreviewImage
    //    {
    //        get => _previewImage;
    //        set => Set(ref _previewImage, value);
    //    }

    //    private readonly CameraLeftUp _cameraLeftUp;
        
    //    public MainWindowViewModel(CameraLeftUp cameraLeftUp)
    //    {
    //        _cameraLeftUp = cameraLeftUp;
          
    //    }


    //    protected override async void OnViewLoaded(object view)
    //    {
    //        await _cameraLeftUp.StartCameraAsync();
    //        _cameraLeftUp.ImageReceived += CameraLeftUpOnImageReceived;
    //        base.OnViewLoaded(view);
    //    }

    //    private void CameraLeftUpOnImageReceived(object? sender, ImagePreviewedEventArgs e)
    //    {
    //        var src = CreateSafeBitmap(e.ImageData, e.Width, e.Height, e.PixelType);
    //        PreviewImage = src;
    //    }

    //    /// 拷贝非托管内存到托管位图，脱离SDK缓冲区限制
    //    private BitmapSource CreateSafeBitmap(IntPtr pData, int w, int h, PixelFormat fmt)
    //    {
    //        if (pData == IntPtr.Zero || w <= 0 || h <= 0)
    //            return null;

    //        int stride = w * fmt.BitsPerPixel / 8;
    //        int bufferByteCount = stride * h;

    //        // 拷贝到托管byte数组，脱离SDK临时内存
    //        byte[] frameBuffer = new byte[bufferByteCount];
    //        Marshal.Copy(pData, frameBuffer, 0, bufferByteCount);

    //        // 用托管数组创建位图，内存完全归CLR管理
    //        BitmapSource bmp = BitmapSource.Create(
    //            w, h, 96, 96, fmt, null, frameBuffer, stride);
    //        bmp.Freeze();
    //        return bmp;
    //    }
    }
}
