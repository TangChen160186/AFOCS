using System.ComponentModel;

namespace AFOCS.Devices.PressureSensor;

public enum PressureChannel: byte
{
    X,
    Y,
    Z,
}
public enum PressureSensorType : byte
{
    [Description("左耦合L")]
    LeftCouplingL,
    [Description("左耦合R")]
    LeftCouplingR,
    [Description("左点胶")]
    LeftDispense,
    [Description("右耦合L")]
    RightCouplingL,
    [Description("右耦合R")]       
    RightCouplingR,
    [Description("右点胶")]
    RightDispense,
}
