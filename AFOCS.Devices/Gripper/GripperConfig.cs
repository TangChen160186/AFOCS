using AFOCS.Infrastructure;

namespace AFOCS.Devices.Gripper;

public class GripperConfig : ICloneable
{
    public ushort SlaveAddress { get; set; }
    public GripperConfig Clone() => new() { SlaveAddress = SlaveAddress };
    object ICloneable.Clone() => Clone();
}

[ConfigPath("设备/夹爪/左工位_耦合左")]
public class LeftCouplingLGripperConfig : GripperConfig
{
    public LeftCouplingLGripperConfig() => SlaveAddress = 1012;
}
[ConfigPath("设备/夹爪/左工位_耦合右")]
public class LeftCouplingRGripperConfig : GripperConfig
{
    public LeftCouplingRGripperConfig() => SlaveAddress = 1013;
}
[ConfigPath("设备/夹爪/右工位_耦合左")]
public class RightCouplingLGripperConfig : GripperConfig
{
    public RightCouplingLGripperConfig() => SlaveAddress = 1030;
}
[ConfigPath("设备/夹爪/右工位_耦合右")]
public class RightCouplingRGripperConfig : GripperConfig
{
    public RightCouplingRGripperConfig() => SlaveAddress = 1031;
}