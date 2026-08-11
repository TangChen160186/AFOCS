using AFOCS.Infrastructure;

namespace AFOCS.Devices.OpticalPowerMeters;

public class OpticalPowerMeterConfig : ICloneable
{
    public string Ip { get; set; } = "192.168.0.200";
    public int Port { get; set; } = 3498;
    public int TimeoutMs { get; set; } = 3000;

    public OpticalPowerMeterConfig Clone() => new()
    {
        Ip = Ip,
        Port = Port,
        TimeoutMs = TimeoutMs,
    };

    object ICloneable.Clone() => Clone();
}

[ConfigPath("设备/功率计/左工位")]
public class OpticalPowerMeterConfigLeft : OpticalPowerMeterConfig
{
    public OpticalPowerMeterConfigLeft() => Ip = "192.168.0.200";
}

[ConfigPath("设备/功率计/右工位")]
public class OpticalPowerMeterConfigRight : OpticalPowerMeterConfig
{
    public OpticalPowerMeterConfigRight() => Ip = "192.168.0.201";
}