using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation
{
    public class CameraConfigLeftDown : HkCameraConfig;
    public class CameraLeftDown(IConfigService configService, ILogger<HkCamera<CameraConfigLeftDown>> logger)
        : HkCamera<CameraConfigLeftDown>(configService, logger);
}
