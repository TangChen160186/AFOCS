using System.ComponentModel;

namespace AFOCS.Devices.Gripper;

public enum GripperType: byte
{
    [Description("左耦合夹爪")]
    LeftCouplingGripper,
    [Description("右耦合夹爪")]
    RightCouplingGripper,
}