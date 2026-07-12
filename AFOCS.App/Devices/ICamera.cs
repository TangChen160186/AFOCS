using AFOCS.App.Core;

namespace AFOCS.App.Devices
{
    public interface ICamera: IDevice
    {
        public int Height { get;}

        public int Width { get; }

        Task<Result> StartCamera();

        Task<Result> StopCamera();

    }
}
