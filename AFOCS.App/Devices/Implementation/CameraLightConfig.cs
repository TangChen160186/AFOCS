namespace AFOCS.App.Devices.Implementation
{
    public class CameraLightConfig
    {
        public string PortName { get; set; } = "COM100";
        public int BaudRate { get; set; }
        public uint Brightness { get; set; }
        public Dictionary<CameraAndLightPos,CameraLightChannel> ChannelMap { get; set; }

        public static CameraLightConfig Default => new CameraLightConfig()
        {
            ChannelMap =new Dictionary<CameraAndLightPos, CameraLightChannel>()
            {
                [CameraAndLightPos.LeftUp] = CameraLightChannel.A,
                [CameraAndLightPos.LeftDown] = CameraLightChannel.B,
                [CameraAndLightPos.RightUp] = CameraLightChannel.C,
                [CameraAndLightPos.RightDown] = CameraLightChannel.D,
            },
            PortName = "COM100",
            BaudRate = 19200,
        };

    }
}
