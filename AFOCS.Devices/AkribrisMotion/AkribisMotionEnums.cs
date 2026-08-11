using System.ComponentModel;

namespace AFOCS.Devices.AkribrisMotion;
public enum AkribisAxisId
{
    X,
    Y,
    Z
}

public enum AkribisMotionType
{
    [Description("左耦合")]
    LeftCoupling,
    [Description("右耦合")]
    RightCoupling,
}
