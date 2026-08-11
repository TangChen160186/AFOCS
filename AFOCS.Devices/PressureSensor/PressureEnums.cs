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
    [Description("左耦合")]
    LeftCoupling,
    [Description("右耦合")]       
    RightCoupling,
    [Description("点胶")]
    Dispense,
}
