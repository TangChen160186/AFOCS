using AFOCS.App.Communication;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation
{
    public class OpticalPowerMeterConfigRight : OpticalPowerMeterConfig;
    public class OpticalPowerMeterRight(
        ITcpClient tcpClient,
        IConfigService configService,
        ILogger<OpticalPowerMeterRight> logger)
        : OpticalPowerMeter<OpticalPowerMeterConfigRight>(tcpClient, configService, logger);
}