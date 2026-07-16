using System.ComponentModel.Composition;
using AFOCS.Communication;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation
{
    public class OpticalPowerMeterConfigRight : OpticalPowerMeterConfig;
    [Export]
    [method: ImportingConstructor]
    public class OpticalPowerMeterRight(
        ITcpClient tcpClient,
        IConfigService configService,
        ILogger logger)
        : OpticalPowerMeter<OpticalPowerMeterConfigRight>(tcpClient, configService, logger);
}