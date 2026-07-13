using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation
{
    public class CameraConfigRightUp : HkCameraConfig;
    public class CameraRightUp(IConfigService configService, ILogger<HkCamera<CameraConfigRightUp>> logger)
        : HkCamera<CameraConfigRightUp>(configService, logger);


}
