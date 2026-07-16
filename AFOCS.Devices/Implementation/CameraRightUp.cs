using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation
{
    public class CameraConfigRightUp : HkCameraConfig;
    [Export]
    [method: ImportingConstructor]
    public class CameraRightUp(IConfigService configService, ILogger logger)
        : Camera<CameraConfigRightUp>(configService, logger);


}
