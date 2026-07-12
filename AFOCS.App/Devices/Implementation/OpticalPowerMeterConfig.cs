namespace AFOCS.App.Devices.Implementation
{
    public class OpticalPowerMeterConfig
    {
        public string Ip { get; set; }

        public int Port { get; set; }

        public static OpticalPowerMeterConfig Default => new OpticalPowerMeterConfig()
        {
            Ip = "192.168.0.200",
            Port = 3498
        };
    }


}
