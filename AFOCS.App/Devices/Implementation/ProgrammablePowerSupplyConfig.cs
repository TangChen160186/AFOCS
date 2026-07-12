namespace AFOCS.App.Devices.Implementation
{
    public class ProgrammablePowerSupplyConfig
    {
        public string VisaAddress { get; set; } = "TCPIP0::127.0.0.1::inst0::INSTR";
        public int TimeoutMs { get; set; } = 3000;
        public static ProgrammablePowerSupplyConfig Default => new ProgrammablePowerSupplyConfig
        {
            VisaAddress = "TCPIP0::127.0.0.1::inst0::INSTR",
            TimeoutMs = 3000
        };
    }
}