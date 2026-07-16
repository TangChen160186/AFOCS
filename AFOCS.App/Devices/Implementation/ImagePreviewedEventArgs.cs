using System.Windows.Media;

namespace AFOCS.App.Devices.Implementation;

public class ImagePreviewedEventArgs(IntPtr data, int width, int height, PixelFormat pixelFormat)
    : EventArgs
{
    public IntPtr ImageData = data;

    public int Width = width;

    public int Height = height;

    public PixelFormat PixelType = pixelFormat;
}