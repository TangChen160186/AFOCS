namespace AFOCS.App.Devices.Implementation
{
    public class OpticalPowerMeterConfigBase
    {
        public string Ip { get; set; }

        public int Port { get; set; }

        public static OpticalPowerMeterConfigBase Default => new OpticalPowerMeterConfigBase()
        {
            Ip = "192.168.0.200",
            Port = 3498
        };
    }

    public class OpticalPowerMeterConfig
    {
        public OpticalPowerMeterConfigBase LeftConfig { get; set; }

        public OpticalPowerMeterConfigBase RightConfig { get; set; }

        public static OpticalPowerMeterConfig Default => new OpticalPowerMeterConfig()
        {
            LeftConfig = OpticalPowerMeterConfigBase.Default,
            RightConfig = OpticalPowerMeterConfigBase.Default
        };
    }


}
