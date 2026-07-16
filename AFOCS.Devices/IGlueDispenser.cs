using AFOCS.Infrastructure;

namespace AFOCS.Devices
{
    public interface IGlueDispenser: IDevice
    {
        Task<Result> ShotAsync();
    }
}
