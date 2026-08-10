using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.PressureSensor;

[Export]
[Export(typeof(IPressureSensor))]
[method: ImportingConstructor]
public class LeftCouplingLPressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor(motionCard, configService, logger)
{
    public override PressureSensorType SensorType => PressureSensorType.LeftCouplingL;
}

[Export(typeof(IPressureSensor))]
[Export]
[method: ImportingConstructor]
public class LeftCouplingRPressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor(motionCard, configService, logger)
{
    public override PressureSensorType SensorType => PressureSensorType.LeftCouplingR;
}

[Export]
[Export(typeof(IPressureSensor))]
[method: ImportingConstructor]
public class LeftDispensePressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor(motionCard, configService, logger)
{
    public override PressureSensorType SensorType => PressureSensorType.LeftDispense;
}

[Export]
[Export(typeof(IPressureSensor))]
[method: ImportingConstructor]
public class RightCouplingLPressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor(motionCard, configService, logger)
{
    public override PressureSensorType SensorType => PressureSensorType.RightCouplingL;
}

[Export]
[Export(typeof(IPressureSensor))]
[method: ImportingConstructor]
public class RightCouplingRPressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor(motionCard, configService, logger)
{
    public override PressureSensorType SensorType => PressureSensorType.RightCouplingR;
}

[Export]
[Export(typeof(IPressureSensor))]
[method: ImportingConstructor]
public class RightDispensePressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor(motionCard, configService, logger)
{
    public override PressureSensorType SensorType => PressureSensorType.RightDispense;
}
