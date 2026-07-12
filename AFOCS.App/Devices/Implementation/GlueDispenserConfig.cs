namespace AFOCS.App.Devices.Implementation
{
    public class GlueDispenserConfig
    {
        public string PortName { get; set; }
        public int BaudRate { get; set; }

        public static GlueDispenserConfig Default => new GlueDispenserConfig
        {
            PortName = "COM100",
            BaudRate = 9600
        };
    }

    public class GlueDispenserConfigLeft: GlueDispenserConfig
    {
    }

    public class GlueDispenserConfigLeftRight: GlueDispenserConfig
    {
    }
}
