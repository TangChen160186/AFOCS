using AFOCS.Communication;
using AFOCS.Infrastructure;
using Serilog;
using System.ComponentModel.Composition;

namespace AFOCS.Devices.Implementation
{
    public class OpticalPowerMeterConfigLeft : OpticalPowerMeterConfig;
    [Export]
    [method: ImportingConstructor]
    public class OpticalPowerMeterLeft(
        ITcpClient tcpClient,
        IConfigService configService,
        ILogger logger)
        : OpticalPowerMeter<OpticalPowerMeterConfigLeft>(tcpClient, configService, logger);

    public class OpticalPowerMeterConfigRight : OpticalPowerMeterConfig;
    [Export]
    [method: ImportingConstructor]
    public class OpticalPowerMeterRight(
        ITcpClient tcpClient,
        IConfigService configService,
        ILogger logger)
        : OpticalPowerMeter<OpticalPowerMeterConfigRight>(tcpClient, configService, logger);
}
