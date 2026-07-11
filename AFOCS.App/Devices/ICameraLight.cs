using AFOCS.App.Core;

namespace AFOCS.App.Devices
{
    public enum CameraLightPos
    {
        LeftUp, RightUp, TopUp, BottomUp,
    }
    public interface ICameraLight:IDevice
    {
        Result Open(CameraLightPos pos);


    }
}
