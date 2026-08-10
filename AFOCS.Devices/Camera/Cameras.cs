using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Camera;


[Export]
[Export(typeof(ICamera))]
[method: ImportingConstructor]
public class CameraLeftUp(IConfigService configService, ILogger logger)
    : Camera<CameraConfigLeftUp>(configService, logger);



[Export]
[Export(typeof(ICamera))]
[method: ImportingConstructor]
public class CameraLeftDown(IConfigService configService, ILogger logger)
    : Camera<CameraConfigLeftDown>(configService, logger);


[Export]
[Export(typeof(ICamera))]
[method: ImportingConstructor]
public class CameraRightDown(IConfigService configService, ILogger logger)
    : Camera<CameraConfigRightDown>(configService, logger);


[Export]
[Export(typeof(ICamera))]
[method: ImportingConstructor]
public class CameraRightUp(IConfigService configService, ILogger logger)
    : Camera<CameraConfigRightUp>(configService, logger);