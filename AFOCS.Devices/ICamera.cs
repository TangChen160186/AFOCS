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
        event EventHandler<ImagePreviewedEventArgs> ImageReceived;

    }
}
