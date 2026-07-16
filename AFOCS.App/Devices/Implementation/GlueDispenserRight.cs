using AFOCS.App.Communication;
using AFOCS.App.Shared;
using Serilog;
using System.ComponentModel.Composition;

namespace AFOCS.App.Devices.Implementation;

public class GlueDispenserConfigRight : GlueDispenserConfig;
[Export]
[method: ImportingConstructor]
public class GlueDispenserRight(
    ISerialPortClient serialPortClient,
    IConfigService configService,
    ILogger logger)
    : GlueDispenser<GlueDispenserConfigRight>(serialPortClient, configService, logger);