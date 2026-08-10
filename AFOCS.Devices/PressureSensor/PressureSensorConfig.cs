using AFOCS.Infrastructure;

namespace AFOCS.Devices.PressureSensor;

public class PressureSensorConfig : ICloneable
{
    public ushort SlaveAddress { get; set; }

    public Dictionary<PressureChannel, ushort> ChannelSubIndexMapping { get; set; } = new()
    {
        [PressureChannel.X] = 1,
        [PressureChannel.Y] = 2,
        [PressureChannel.Z] = 3,
    };

    public Dictionary<PressureChannel, int> AlarmThresholds { get; set; } = new()
    {
        [PressureChannel.X] = 400,
        [PressureChannel.Y] = 400,
        [PressureChannel.Z] = 400,
    };

    public ushort GetSubIndex(PressureChannel channel) =>
        ChannelSubIndexMapping[channel];

    public int GetAlarmThreshold(PressureChannel channel) =>
        AlarmThresholds[channel];

    public PressureSensorConfig Clone() => new()
    {
        SlaveAddress = SlaveAddress,
        ChannelSubIndexMapping = new Dictionary<PressureChannel, ushort>(ChannelSubIndexMapping),
        AlarmThresholds = new Dictionary<PressureChannel, int>(AlarmThresholds),
    };

    object ICloneable.Clone() => Clone();
}

[ConfigPath("设备/压力传感器/左工位_耦合左")]
public class LeftCouplingLPressureSensorConfig : PressureSensorConfig
{
    public LeftCouplingLPressureSensorConfig() => SlaveAddress = 1014;
}

[ConfigPath("设备/压力传感器/左工位_耦合右")]
public class LeftCouplingRPressureSensorConfig : PressureSensorConfig
{
    public LeftCouplingRPressureSensorConfig() => SlaveAddress = 1015;
}

[ConfigPath("设备/压力传感器/左工位_点胶")]
public class LeftDispensePressureSensorConfig : PressureSensorConfig
{
    public LeftDispensePressureSensorConfig() => SlaveAddress = 1016;
}

[ConfigPath("设备/压力传感器/右工位_耦合左")]
public class RightCouplingLPressureSensorConfig : PressureSensorConfig
{
    public RightCouplingLPressureSensorConfig() => SlaveAddress = 1017;
}

[ConfigPath("设备/压力传感器/右工位_耦合右")]
public class RightCouplingRPressureSensorConfig : PressureSensorConfig
{
    public RightCouplingRPressureSensorConfig() => SlaveAddress = 1018;
}

[ConfigPath("设备/压力传感器/右工位_点胶")]
public class RightDispensePressureSensorConfig : PressureSensorConfig
{
    public RightDispensePressureSensorConfig() => SlaveAddress = 1019;
}