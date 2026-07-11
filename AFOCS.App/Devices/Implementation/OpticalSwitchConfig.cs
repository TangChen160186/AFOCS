namespace AFOCS.App.Devices.Implementation
{
    public class OpticalSwitchConfig
    {
        public string Ip { get; set; }

        public int Port { get; set; }

        public static OpticalSwitchConfig Default => new OpticalSwitchConfig()
        {
            Ip = "192.168.0.200",
            Port = 3498
        };
    }
}
