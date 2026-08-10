using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Devices.MotionControlCard;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Gripper;

[Export]
[Export(typeof(IGripper))]
[Description("左工位_左耦合_夹爪")]
[method: ImportingConstructor]
public class LeftCouplingLGripper(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : GripperBase<LeftCouplingLGripperConfig>(motionCard, configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Left;
    public override GripperType GripperType => GripperType.LeftCouplingGripper;
}

[Export]
[Export(typeof(IGripper))]
[Description("左工位_右耦合_夹爪")]
[method: ImportingConstructor]
public class LeftCouplingRGripper(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : GripperBase<LeftCouplingRGripperConfig>(motionCard, configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Left;
    public override GripperType GripperType => GripperType.RightCouplingGripper;
}

[Export]
[Export(typeof(IGripper))]
[Description("右工位_左耦合_夹爪")]
[method: ImportingConstructor]
public class RightCouplingLGripper(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : GripperBase<RightCouplingLGripperConfig>(motionCard, configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Right;
    public override GripperType GripperType => GripperType.LeftCouplingGripper;
}

[Export]
[Export(typeof(IGripper))]
[Description("右工位_右耦合_夹爪")]
[method: ImportingConstructor]
public class RightCouplingRGripper(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : GripperBase<RightCouplingRGripperConfig>(motionCard, configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Right;
    public override GripperType GripperType => GripperType.RightCouplingGripper;
}
