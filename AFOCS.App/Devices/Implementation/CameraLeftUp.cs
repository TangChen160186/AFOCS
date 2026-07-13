using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation
{
    public class CameraConfigLeftUp : HkCameraConfig;
    public class CameraLeftUp(IConfigService configService, ILogger<HkCamera<CameraConfigLeftUp>> logger)
        : HkCamera<CameraConfigLeftUp>(configService, logger);

}
