using AFOCS.Devices.Implementation;
using AFOCS.Infrastructure;

namespace AFOCS.Devices
{
    public interface IHeightGauge : IDevice
    {
        HeightGaugeConfig GetConfig();
        Task SaveConfigAsync(HeightGaugeConfig config);
        Task<Result<double>> GetHeightAsync(int channel);
    }
}
