using AFOCS.App.Core;

namespace AFOCS.App.Devices
{
    public interface IHeightGauge : IDevice
    {
        Task<Result<double>> GetHeightAsync(int channel);
    }
}
