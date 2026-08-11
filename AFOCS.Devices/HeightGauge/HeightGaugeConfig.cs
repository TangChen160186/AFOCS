using AFOCS.Infrastructure;

namespace AFOCS.Devices.HeightGauge;

[ConfigPath("设备/测高仪")]
public class HeightGaugeConfig : ICloneable
{
    public string Ip { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 1000;
    public int TimeoutMs { get; set; } = 3000;

    public HeightGaugeConfig Clone() => new()
    {
        Ip = Ip,
        Port = Port,
        TimeoutMs = TimeoutMs,
    };

    object ICloneable.Clone() => Clone();
}