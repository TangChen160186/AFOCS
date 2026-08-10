using AFOCS.Infrastructure;

namespace AFOCS.Devices.Camera;


public class HkCameraConfig : ICloneable
{
    public string ChSerialNumber { get; set; } = "ChSerialNumber";

    /// <summary>相机精度 (mm/pixel)，上相机 0.0023，侧相机 0.0018</summary>
    public virtual double Precision { get; set; } = 0;

    public HkCameraConfig Clone() => new()
    {
        ChSerialNumber = ChSerialNumber,
        Precision = Precision,
    };

    object ICloneable.Clone() => Clone();
}
[ConfigPath("设备/相机/左工位_上相机")]
public class CameraConfigLeftUp : HkCameraConfig
{
    public override double Precision { get; set; } = 0.0023;
}
[ConfigPath("设备/相机/左工位_下相机")]
public class CameraConfigLeftDown : HkCameraConfig
{
    public override double Precision { get; set; } = 0.0018;
}

[ConfigPath("设备/相机/右工位_上相机")]
public class CameraConfigRightUp : HkCameraConfig
{
    public override double Precision { get; set; } = 0.0023;
}

[ConfigPath("设备/相机/右工位_下相机")]
public class CameraConfigRightDown : HkCameraConfig
{
    public override double Precision { get; set; } = 0.0018;
}
