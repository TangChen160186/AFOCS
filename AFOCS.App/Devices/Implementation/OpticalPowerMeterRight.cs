using AFOCS.App.Communication;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation;

public class OpticalPowerMeterRight : OpticalPowerMeter
{
    public OpticalPowerMeterRight(ITcpClient tcpClient, IConfigService configService, ILogger<OpticalPowerMeter> logger) : base(tcpClient, configService, logger)
    {
    }
}