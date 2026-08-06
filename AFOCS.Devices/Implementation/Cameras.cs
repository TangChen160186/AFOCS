using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation
{
    public class CameraConfigLeftUp : HkCameraConfig
    {
        public override double Precision { get; set; } = 0.0023;
    }
    [Export]
    [Export(typeof(ICamera))]
    [method: ImportingConstructor]
    public class CameraLeftUp(IConfigService configService, ILogger logger)
        : Camera<CameraConfigLeftUp>(configService, logger);


    public class CameraConfigLeftDown : HkCameraConfig
    {
        public override double Precision { get; set; } = 0.0018;
    }
    [Export]
    [Export(typeof(ICamera))]
    [method: ImportingConstructor]
    public class CameraLeftDown(IConfigService configService, ILogger logger)
        : Camera<CameraConfigLeftDown>(configService, logger);

    public class CameraConfigRightDown : HkCameraConfig
    {
        public override double Precision { get; set; } = 0.0018;
    }
    [Export]
    [Export(typeof(ICamera))]
    [method: ImportingConstructor]
    public class CameraRightDown(IConfigService configService, ILogger logger)
        : Camera<CameraConfigRightDown>(configService, logger);

    public class CameraConfigRightUp : HkCameraConfig
    {
        public override double Precision { get; set; } = 0.0023;
    }
    [Export]
    [Export(typeof(ICamera))]
    [method: ImportingConstructor]
    public class CameraRightUp(IConfigService configService, ILogger logger)
        : Camera<CameraConfigRightUp>(configService, logger);
}

