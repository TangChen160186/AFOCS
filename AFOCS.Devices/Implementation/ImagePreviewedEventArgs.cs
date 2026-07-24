namespace AFOCS.Devices.Implementation;

public class ImagePreviewedEventArgs(IntPtr data, int width, int height, bool isMono)
    : EventArgs
{
    public IntPtr ImageData = data;

    public int Width = width;

    public int Height = height;

    public bool IsMono = isMono;
}
