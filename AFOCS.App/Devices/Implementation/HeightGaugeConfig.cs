namespace AFOCS.App.Devices.Implementation
{
    public class HeightGaugeConfig
    {
        public string Ip { get; set; }
        public int Port { get; set; }

        public static HeightGaugeConfig Default => new HeightGaugeConfig()
        {
            Ip = "127.0.0.1",
            Port = 1000,
        };
    }
}
