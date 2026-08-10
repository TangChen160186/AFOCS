using AFOCS.Infrastructure;

namespace AFOCS.Devices.HeightGauge;

public interface IHeightGauge : IDevice
{
    HeightGaugeConfig GetConfig();
    Task SaveConfigAsync(HeightGaugeConfig config);
    Task<Result<double>> GetHeightAsync(int channel);
}