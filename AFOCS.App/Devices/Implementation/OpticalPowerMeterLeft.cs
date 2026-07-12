using AFOCS.App.Communication;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation
{
    public class OpticalPowerMeterConfigLeft : OpticalPowerMeterConfig;

    public class OpticalPowerMeterLeft(
        ITcpClient tcpClient,
        IConfigService configService,
        ILogger<OpticalPowerMeterLeft> logger)
        : OpticalPowerMeter<OpticalPowerMeterConfigLeft>(tcpClient, configService, logger);
}