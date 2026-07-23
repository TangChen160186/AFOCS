using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation;

// ============================================================
// 6 个压力传感器的独立配置类型（确保 ConfigService 各自存取）
// ============================================================

public class LeftCouplingLPressureSensorConfig : PressureSensorConfig
{
    public LeftCouplingLPressureSensorConfig() => SlaveAddress = 1014;
}

public class LeftCouplingRPressureSensorConfig : PressureSensorConfig
{
    public LeftCouplingRPressureSensorConfig() => SlaveAddress = 1015;
}

public class LeftDispensePressureSensorConfig : PressureSensorConfig
{
    public LeftDispensePressureSensorConfig() => SlaveAddress = 1016;
}

public class RightCouplingLPressureSensorConfig : PressureSensorConfig
{
    public RightCouplingLPressureSensorConfig() => SlaveAddress = 1017;
}

public class RightCouplingRPressureSensorConfig : PressureSensorConfig
{
    public RightCouplingRPressureSensorConfig() => SlaveAddress = 1018;
}

public class RightDispensePressureSensorConfig : PressureSensorConfig
{
    public RightDispensePressureSensorConfig() => SlaveAddress = 1019;
}

// ============================================================
// 6 个压力传感器 MEF 导出
// ============================================================
[Export]
[Export(typeof(IPressureSensor))]
[method: ImportingConstructor]
public class LeftCouplingLPressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor(motionCard, configService, logger)
{
    public override string DisplayName => "左耦合左压力传感器";
    protected override ushort DefaultSlaveAddress => 1014;
    protected override Type ConfigType => typeof(LeftCouplingLPressureSensorConfig);
}

[Export(typeof(IPressureSensor))]
[Export]
[method: ImportingConstructor]
public class LeftCouplingRPressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor(motionCard, configService, logger)
{
    public override string DisplayName => "左耦合右压力传感器";
    protected override ushort DefaultSlaveAddress => 1015;
    protected override Type ConfigType => typeof(LeftCouplingRPressureSensorConfig);
}
[Export]
[Export(typeof(IPressureSensor))]
[method: ImportingConstructor]
public class LeftDispensePressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor(motionCard, configService, logger)
{
    public override string DisplayName => "左点胶压力传感器";
    protected override ushort DefaultSlaveAddress => 1016;
    protected override Type ConfigType => typeof(LeftDispensePressureSensorConfig);
}
[Export]
[Export(typeof(IPressureSensor))]
[method: ImportingConstructor]
public class RightCouplingLPressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor(motionCard, configService, logger)
{
    public override string DisplayName => "右耦合左压力传感器";
    protected override ushort DefaultSlaveAddress => 1017;
    protected override Type ConfigType => typeof(RightCouplingLPressureSensorConfig);
}
[Export]
[Export(typeof(IPressureSensor))]
[method: ImportingConstructor]
public class RightCouplingRPressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor(motionCard, configService, logger)
{
    public override string DisplayName => "右耦合右压力传感器";
    protected override ushort DefaultSlaveAddress => 1018;
    protected override Type ConfigType => typeof(RightCouplingRPressureSensorConfig);
}
[Export]
[Export(typeof(IPressureSensor))]
[method: ImportingConstructor]
public class RightDispensePressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : PressureSensor(motionCard, configService, logger)
{
    public override string DisplayName => "右点胶压力传感器";
    protected override ushort DefaultSlaveAddress => 1019;
    protected override Type ConfigType => typeof(RightDispensePressureSensorConfig);
}
