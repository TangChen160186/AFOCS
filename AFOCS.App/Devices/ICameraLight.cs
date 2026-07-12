using AFOCS.App.Core;

namespace AFOCS.App.Devices
{
    public enum CameraAndLightPos: byte
    {
        LeftUp, 
        LeftDown, 
        RightUp, 
        RightDown,
    }

    public enum CameraLightChannel : byte
    {
        A,
        B,
        C,
        D
    }
    public interface ICameraLight:IDevice
    {
        Task<Result> OpenAsync(CameraAndLightPos pos);

        Task<Result> SetLightBrightnessAsync(CameraAndLightPos pos,uint brightness);
    }
}
