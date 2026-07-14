using AFOCS.App.Core;

namespace AFOCS.App.Devices
{
    public interface IMotionControlCard: IDevice
    {

        Task<Result> HotResetAsync();

        
    }
}
