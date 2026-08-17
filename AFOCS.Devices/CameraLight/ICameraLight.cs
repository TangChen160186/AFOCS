using AFOCS.Infrastructure;

namespace AFOCS.Devices.CameraLight;

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
    Task<Result> SetLightBrightnessAsync(CameraLightChannel channel, byte brightness);
}