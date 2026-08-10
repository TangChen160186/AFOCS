using AFOCS.Infrastructure;

namespace AFOCS.Devices;

public interface IDevice : IDisposable
{
    bool IsConnected { get;}

    WorkPos WorkPos { get; }

    Task<Result> InitializeAsync(CancellationToken token = default);
    Task<Result> ReConnectAsync(CancellationToken token = default);
}

