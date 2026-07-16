using System.ComponentModel.Composition;
using AFOCS.Communication;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation
{
    public class OpticalPowerMeterConfigLeft : OpticalPowerMeterConfig;
    [Export]
    [method:ImportingConstructor]
    public class OpticalPowerMeterLeft(
        ITcpClient tcpClient,
        IConfigService configService,
        ILogger logger)
        : OpticalPowerMeter<OpticalPowerMeterConfigLeft>(tcpClient, configService, logger);
}