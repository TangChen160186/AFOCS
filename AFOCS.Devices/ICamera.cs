using AFOCS.Devices.Implementation;
using AFOCS.Infrastructure;

namespace AFOCS.Devices
{
    public interface ICamera: IDevice
    {
        public uint Height { get;}

        public uint Width { get; }

        public uint WidthStep { get; }

        public uint HeightStep { get; }

        HkCameraConfig GetConfig();
        Task SaveConfigAsync(HkCameraConfig config);

        Task<Result> StartCameraAsync();

        Task<Result> StopCameraAsync();

        Task<Result> SoftwareTriggerOnce();
        Task<Result<string>> CaptureImageAsync(string filePath);

        /// <summary>获取最新一帧的原始像素数据（byte[]、宽高、是否单色）</summary>
        Task<Result<(byte[] Data, int Width, int Height, bool IsMono)>> GrabFrameAsync();

        event EventHandler<ImagePreviewedEventArgs> ImageReceived;

    }
}
