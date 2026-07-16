using AFOCS.Infrastructure;

namespace AFOCS.Devices
{
    public interface IHeightGauge : IDevice
    {
        Task<Result<double>> GetHeightAsync(int channel);
    }
}
