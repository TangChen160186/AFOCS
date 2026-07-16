using AFOCS.Infrastructure;

namespace AFOCS.Devices
{
    public interface IMotionControlCard: IDevice
    {

        Task<Result> HotResetAsync();

        
    }
}
