using AFOCS.App.Core;
using AFOCS.App.Devices.Implementation;

namespace AFOCS.App.Devices
{
    public interface ICamera: IDevice
    {
        public uint Height { get;}

        public uint Width { get; }

        public uint WidthStep { get; }

        public uint HeightStep { get; }

        Task<Result> StartCameraAsync();

        Task<Result> StopCameraAsync();

        Task<Result> SoftwareTriggerOnce();
        event EventHandler<ImagePreviewedEventArgs> ImageReceived;

    }
}
