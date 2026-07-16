using System.ComponentModel.Composition;
using AFOCS.Communication;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation;

public class GlueDispenserConfigRight : GlueDispenserConfig;
[Export]
[method: ImportingConstructor]
public class GlueDispenserRight(
    ISerialPortClient serialPortClient,
    IConfigService configService,
    ILogger logger)
    : GlueDispenser<GlueDispenserConfigRight>(serialPortClient, configService, logger);