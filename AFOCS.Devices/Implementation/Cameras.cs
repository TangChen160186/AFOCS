using AFOCS.Infrastructure;
using Serilog;
using System.ComponentModel.Composition;

namespace AFOCS.Devices.Implementation
{
    public class CameraConfigLeftUp : HkCameraConfig;
    [Export]
    [method: ImportingConstructor]
    public class CameraLeftUp(IConfigService configService, ILogger logger)
        : Camera<CameraConfigLeftUp>(configService, logger);


    public class CameraConfigLeftDown : HkCameraConfig;
    [Export]
    [method: ImportingConstructor]
    public class CameraLeftDown(IConfigService configService, ILogger logger)
        : Camera<CameraConfigLeftDown>(configService, logger);

    public class CameraConfigRightDown : HkCameraConfig;
    [Export]
    [method: ImportingConstructor]
    public class CameraRightDown(IConfigService configService, ILogger logger)
        : Camera<CameraConfigRightDown>(configService, logger);

    public class CameraConfigRightUp : HkCameraConfig;
    [Export]
    [method: ImportingConstructor]
    public class CameraRightUp(IConfigService configService, ILogger logger)
        : Camera<CameraConfigRightUp>(configService, logger);
}

