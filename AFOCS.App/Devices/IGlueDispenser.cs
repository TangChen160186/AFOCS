using Result = AFOCS.App.Core.Result;

namespace AFOCS.App.Devices
{
    public interface IGlueDispenser: IDevice
    {
        Task<Result> ShotAsync();
    }
}
