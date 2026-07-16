using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation
{
    public class CameraConfigLeftUp : HkCameraConfig;
    [Export]
    [method: ImportingConstructor]
    public class CameraLeftUp(IConfigService configService, ILogger logger)
        : Camera<CameraConfigLeftUp>(configService, logger);

}
