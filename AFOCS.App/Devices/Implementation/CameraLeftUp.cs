using System.ComponentModel.Composition;
using AFOCS.App.Shared;
using Serilog;

namespace AFOCS.App.Devices.Implementation
{
    public class CameraConfigLeftUp : HkCameraConfig;
    [Export]
    [method: ImportingConstructor]
    public class CameraLeftUp(IConfigService configService, ILogger logger)
        : Camera<CameraConfigLeftUp>(configService, logger);

}
