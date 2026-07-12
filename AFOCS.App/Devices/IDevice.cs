using AFOCS.App.Enums;
using Result = AFOCS.App.Core.Result;

namespace AFOCS.App.Devices
{
    public interface IDevice : IDisposable
    {
        bool IsConnected { get;}
        EDeviceType Type { get; }
        Task<Result> InitializeAsync(CancellationToken token = default);

        Task<Result> StopAsync(CancellationToken token = default);
        Task<Result> ReConnectAsync(CancellationToken token = default);
    }
}
