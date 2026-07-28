using AFOCS.Devices.Implementation;
using AFOCS.Infrastructure;

namespace AFOCS.Devices;

public enum CameraLightChannel : byte
{
    A,
    B,
    C,
    D
}
public interface ICameraLight:IDevice
{
    CameraLightConfig GetConfig();
    Task SaveConfigAsync(CameraLightConfig config);
    Task<Result> SetLightBrightnessAsync(CameraLightChannel channel, uint brightness);
}