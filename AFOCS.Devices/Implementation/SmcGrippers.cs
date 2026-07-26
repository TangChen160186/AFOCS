using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation;

// ============================================================
// 4 个夹爪的独立配置类型（确保 ConfigService 各自存取）
// ============================================================

public class LeftCouplingLGripperConfig : SmcGripperConfig
{
    public LeftCouplingLGripperConfig() => SlaveAddress = 1012;
}

public class LeftCouplingRGripperConfig : SmcGripperConfig
{
    public LeftCouplingRGripperConfig() => SlaveAddress = 1013;
}

public class RightCouplingLGripperConfig : SmcGripperConfig
{
    public RightCouplingLGripperConfig() => SlaveAddress = 1030;
}

public class RightCouplingRGripperConfig : SmcGripperConfig
{
    public RightCouplingRGripperConfig() => SlaveAddress = 1031;
}

// ============================================================
// 4 个夹爪 MEF 导出
// ============================================================

[Export]
[Export(typeof(ISmcGripper))]
[method: ImportingConstructor]
public class LeftCouplingLGripper(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : SmcGripperBase(motionCard, configService, logger)
{
    public override string DisplayName => "左耦合左夹爪";
    protected override ushort DefaultSlaveAddress => 1012;
    protected override Type ConfigType => typeof(LeftCouplingLGripperConfig);
}

[Export]
[Export(typeof(ISmcGripper))]
[method: ImportingConstructor]
public class LeftCouplingRGripper(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : SmcGripperBase(motionCard, configService, logger)
{
    public override string DisplayName => "左耦合右夹爪";
    protected override ushort DefaultSlaveAddress => 1013;
    protected override Type ConfigType => typeof(LeftCouplingRGripperConfig);
}

[Export]
[Export(typeof(ISmcGripper))]
[method: ImportingConstructor]
public class RightCouplingLGripper(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : SmcGripperBase(motionCard, configService, logger)
{
    public override string DisplayName => "右耦合左夹爪";
    protected override ushort DefaultSlaveAddress => 1030;
    protected override Type ConfigType => typeof(RightCouplingLGripperConfig);
}

[Export]
[Export(typeof(ISmcGripper))]
[method: ImportingConstructor]
public class RightCouplingRGripper(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : SmcGripperBase(motionCard, configService, logger)
{
    public override string DisplayName => "右耦合右夹爪";
    protected override ushort DefaultSlaveAddress => 1031;
    protected override Type ConfigType => typeof(RightCouplingRGripperConfig);
}
