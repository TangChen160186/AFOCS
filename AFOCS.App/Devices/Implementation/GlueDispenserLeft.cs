using System.ComponentModel.Composition;
using AFOCS.App.Communication;
using AFOCS.App.Shared;
using Serilog;

namespace AFOCS.App.Devices.Implementation;

public class GlueDispenserConfigLeft : GlueDispenserConfig;
[Export]
[method: ImportingConstructor]
public class GlueDispenserLeft(
    ISerialPortClient serialPortClient,
    IConfigService configService,
    ILogger logger)
    : GlueDispenser<GlueDispenserConfigLeft>(serialPortClient, configService, logger);