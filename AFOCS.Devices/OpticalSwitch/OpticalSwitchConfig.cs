using AFOCS.Infrastructure;

namespace AFOCS.Devices.OpticalSwitch;

[ConfigPath("设备/光开关")]
public class OpticalSwitchConfig : ICloneable
{
    public string Ip { get; set; } = "192.168.1.188";
    public int Port { get; set; } = 1000;
    public int TimeoutMs { get; set; } = 3000;

    public OpticalSwitchConfig Clone() => new()
    {
        Ip = Ip,
        Port = Port,
        TimeoutMs = TimeoutMs,
    };

    object ICloneable.Clone() => Clone();
}