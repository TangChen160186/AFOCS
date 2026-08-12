using HalconDotNet;

namespace AFOCS.VisionEditor.Models;

/// <summary>
/// 像素数据：用于节点间传递原始图像数据，代替 Emgu.CV Mat。
/// </summary>
public record PixelData(byte[] Data, int Width, int Height, int Channels)
{
    /// <summary>从 byte[] 创建 HImage</summary>
    public HImage ToHImage()
    {
        unsafe
        {
            fixed (byte* ptr = Data)
            {
                return new HImage("byte", Width, Height, (IntPtr)ptr);
            }
        }
    }
}
