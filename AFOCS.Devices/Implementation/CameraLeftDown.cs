using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation
{
    public class CameraConfigLeftDown : HkCameraConfig;
    [Export]
    [method: ImportingConstructor]
    public class CameraLeftDown(IConfigService configService, ILogger logger)
        : Camera<CameraConfigLeftDown>(configService, logger);
}
