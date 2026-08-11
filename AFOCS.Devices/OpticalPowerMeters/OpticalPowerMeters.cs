using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Communication;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.OpticalPowerMeters;

[Export]
[Export(typeof(IOpticalPowerMeter))]
[Description("左工位功率计")]
[method: ImportingConstructor]
public class OpticalPowerMeterLeft(
    ITcpClient tcpClient,
    IConfigService configService,
    ILogger logger)
    : OpticalPowerMeter<OpticalPowerMeterConfigLeft>(tcpClient, configService, logger);


[Export]
[Export(typeof(IOpticalPowerMeter))]
[Description("右工位功率计")]
[method: ImportingConstructor]
public class OpticalPowerMeterRight(
    ITcpClient tcpClient,
    IConfigService configService,
    ILogger logger)
    : OpticalPowerMeter<OpticalPowerMeterConfigRight>(tcpClient, configService, logger);