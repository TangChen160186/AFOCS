using AFOCS.App.Communication;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation;

public class GlueDispenserConfigLeft : GlueDispenserConfig;
public class GlueDispenserLeft(
    ISerialPortClient serialPortClient,
    IConfigService configService,
    ILogger<GlueDispenserLeft> logger)
    : GlueDispenser<GlueDispenserConfigLeft>(serialPortClient, configService, logger);