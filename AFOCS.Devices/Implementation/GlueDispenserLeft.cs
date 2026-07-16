using System.ComponentModel.Composition;
using AFOCS.Communication;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation;

public class GlueDispenserConfigLeft : GlueDispenserConfig;
[Export]
[method: ImportingConstructor]
public class GlueDispenserLeft(
    ISerialPortClient serialPortClient,
    IConfigService configService,
    ILogger logger)
    : GlueDispenser<GlueDispenserConfigLeft>(serialPortClient, configService, logger);