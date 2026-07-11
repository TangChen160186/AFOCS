namespace AFOCS.App.Devices.Implementation
{
    public class GlueDispenserConfigBase
    {
        public string PortName { get; set; }

        public int BaudRate { get; set; }


        public static GlueDispenserConfigBase Default => new GlueDispenserConfigBase
        {
            PortName = "COM100",
            BaudRate = 9600
        };
    }

    public class GlueDispenserConfig
    {
        public GlueDispenserConfigBase LeftConfig { get; set; }
        public GlueDispenserConfigBase RightConfig { get; set; }

        public static GlueDispenserConfig Default => new GlueDispenserConfig
        {
            LeftConfig = GlueDispenserConfigBase.Default,
            RightConfig = GlueDispenserConfigBase.Default
        };
    }
}
