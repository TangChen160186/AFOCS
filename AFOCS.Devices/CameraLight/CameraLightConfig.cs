using AFOCS.Infrastructure;

namespace AFOCS.Devices.CameraLight;

[ConfigPath("设备/相机光源")]
public class CameraLightConfig : ICloneable
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 19200;
    public int TimeoutMs { get; set; } = 3000;

    public CameraLightConfig Clone() => new()
    {
        PortName = PortName,
        BaudRate = BaudRate,
        TimeoutMs = TimeoutMs,
    };

    object ICloneable.Clone() => Clone();
}