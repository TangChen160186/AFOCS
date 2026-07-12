using AFOCS.App.Core;

namespace AFOCS.App.Devices;

public enum CameraLightChannel : byte
{
    A,
    B,
    C,
    D
}
public interface ICameraLight:IDevice
{
    Task<Result> SetLightBrightnessAsync(CameraLightChannel channel, uint brightness);
}