using AFOCS.Infrastructure;

namespace AFOCS.Devices.ProgrammablePowerSupply;

[ConfigPath("设备/编程电源")]
public class ProgrammablePowerSupplyConfig : ICloneable
{
    public string VisaAddress { get; set; } = "TCPIP0::127.0.0.1::inst0::INSTR";
    public int TimeoutMs { get; set; } = 3000;

    public ProgrammablePowerSupplyConfig Clone() => new()
    {
        VisaAddress = VisaAddress,
        TimeoutMs = TimeoutMs,
    };

    object ICloneable.Clone() => Clone();
}