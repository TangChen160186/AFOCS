using System.ComponentModel.Composition;
using AFOCS.App.Shared;
using Serilog;

namespace AFOCS.App.Devices.Implementation
{
    public class CameraConfigRightDown : HkCameraConfig;
    [Export]
    [method: ImportingConstructor]
    public class CameraRightDown(IConfigService configService, ILogger logger)
        : Camera<CameraConfigRightDown>(configService, logger);
}