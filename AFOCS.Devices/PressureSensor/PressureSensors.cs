using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Devices.MotionControlCard;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.PressureSensor;

[Export]
[Export(typeof(IPressureSensor))]
[method: ImportingConstructor]
[Description("左工位_左耦合_压力传感器")]
public class LeftCouplingLPressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor<LeftCouplingLPressureSensorConfig>(motionCard, configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Left;
    public override PressureSensorType SensorType => PressureSensorType.LeftCoupling;
}

[Export(typeof(IPressureSensor))]
[Export]
[Description("左工位_右耦合_压力传感器")]
[method: ImportingConstructor]
public class LeftCouplingRPressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor<LeftCouplingRPressureSensorConfig>(motionCard, configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Left;
    public override PressureSensorType SensorType => PressureSensorType.RightCoupling;
}

[Export]
[Export(typeof(IPressureSensor))]
[Description("左工位_点胶_压力传感器")]
[method: ImportingConstructor]
public class LeftDispensePressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor<LeftDispensePressureSensorConfig>(motionCard, configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Left;
    public override PressureSensorType SensorType => PressureSensorType.Dispense;
}

[Export]
[Export(typeof(IPressureSensor))]
[Description("右工位_左耦合_压力传感器")]
[method: ImportingConstructor]
public class RightCouplingLPressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor<RightCouplingLPressureSensorConfig>(motionCard, configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Right;
    public override PressureSensorType SensorType => PressureSensorType.LeftCoupling;
}

[Export]
[Export(typeof(IPressureSensor))]
[Description("右工位_右耦合_压力传感器")]
[method: ImportingConstructor]
public class RightCouplingRPressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor<RightCouplingRPressureSensorConfig>(motionCard, configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Right;
    public override PressureSensorType SensorType => PressureSensorType.RightCoupling;
}

[Export]
[Export(typeof(IPressureSensor))]
[Description("右工位_点胶_压力传感器")]
[method: ImportingConstructor]
public class RightDispensePressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor<RightDispensePressureSensorConfig>(motionCard, configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Right;
    public override PressureSensorType SensorType => PressureSensorType.Dispense;
}
