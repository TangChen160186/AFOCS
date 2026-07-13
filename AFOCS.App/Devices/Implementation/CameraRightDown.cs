using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation
{
    public class CameraConfigRightDown : HkCameraConfig;
    public class CameraRightDown(IConfigService configService, ILogger<HkCamera<CameraConfigRightDown>> logger)
        : HkCamera<CameraConfigRightDown>(configService, logger);
}