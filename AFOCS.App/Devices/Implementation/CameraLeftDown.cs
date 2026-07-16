using System.ComponentModel.Composition;
using AFOCS.App.Shared;
using Serilog;

namespace AFOCS.App.Devices.Implementation
{
    public class CameraConfigLeftDown : HkCameraConfig;
    [Export]
    [method: ImportingConstructor]
    public class CameraLeftDown(IConfigService configService, ILogger logger)
        : Camera<CameraConfigLeftDown>(configService, logger);
}
