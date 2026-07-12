using AFOCS.App.Communication;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation;

public class GlueDispenserConfigRight : GlueDispenserConfig;
public class GlueDispenserRight(
    ISerialPortClient serialPortClient,
    IConfigService configService,
    ILogger<GlueDispenserRight> logger)
    : GlueDispenser<GlueDispenserConfigRight>(serialPortClient, configService, logger);