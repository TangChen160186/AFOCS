namespace AFOCS.App.Devices
{
    public class OpticalSwitchConfigBase
    {
        public string Ip { get; set; }

        public int Port { get; set; }

        public static OpticalSwitchConfigBase Default => new OpticalSwitchConfigBase()
        {
            Ip = "192.168.0.200",
            Port = 3498
        };
    }

    public class OpticalSwitchConfig
    {
        public OpticalSwitchConfigBase LeftConfig { get; set; }

        public OpticalSwitchConfigBase RightConfig { get; set; }

        public static OpticalSwitchConfig Default => new OpticalSwitchConfig()
        {
            LeftConfig = OpticalSwitchConfigBase.Default,
            RightConfig = OpticalSwitchConfigBase.Default
        };
    }
}
