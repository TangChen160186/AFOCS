using AFOCS.App.Communication;
using AFOCS.App.Shared;
using Serilog;
using System.ComponentModel.Composition;

namespace AFOCS.App.Devices.Implementation
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