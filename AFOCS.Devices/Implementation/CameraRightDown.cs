using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation
{
    public class CameraConfigRightDown : HkCameraConfig;
    [Export]
    [method: ImportingConstructor]
    public class CameraRightDown(IConfigService configService, ILogger logger)
        : Camera<CameraConfigRightDown>(configService, logger);
}