using System.ComponentModel.Composition;
using AFOCS.App.Communication;
using AFOCS.App.Shared;
using Serilog;

namespace AFOCS.App.Devices.Implementation
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