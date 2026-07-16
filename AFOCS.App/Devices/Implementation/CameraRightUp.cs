using System.ComponentModel.Composition;
using AFOCS.App.Shared;
using Serilog;

namespace AFOCS.App.Devices.Implementation
{
    public class CameraConfigRightUp : HkCameraConfig;
    [Export]
    [method: ImportingConstructor]
    public class CameraRightUp(IConfigService configService, ILogger logger)
        : Camera<CameraConfigRightUp>(configService, logger);


}
