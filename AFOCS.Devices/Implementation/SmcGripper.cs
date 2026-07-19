using AFOCS.Infrastructure;

namespace AFOCS.Devices.Implementation
{
    internal class SmcGripper: IDevice
    {
        public bool IsConnected { get; }
        public void Dispose()
        {
            // TODO release managed resources here
        }

     
        public Task<Result> InitializeAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result> StopAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result> ReConnectAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
    }
}
