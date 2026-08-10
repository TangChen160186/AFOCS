using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.AkribrisMotion;


[Export]
[Export(typeof(IAkribisMotion))]
[Description("左工位_耦合左")]
[method: ImportingConstructor]
public sealed class AkribisLeftCouplingL(IConfigService configService, ILogger logger)
    : AkribisMotion<LeftCouplingLConfig>(configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Left;
    public override AkribisMotionType AkribisMotionType => AkribisMotionType.LeftCoupling;
}

[Export]
[Export(typeof(IAkribisMotion))]
[Description("左工位_耦合右")]
[method: ImportingConstructor]
public sealed class AkribisLeftCouplingR(IConfigService configService, ILogger logger)
    : AkribisMotion<LeftCouplingRConfig>(configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Right;
    public override AkribisMotionType AkribisMotionType => AkribisMotionType.RightCoupling;
}

[Export]
[Export(typeof(IAkribisMotion))]
[Description("右工位_耦合左")]
[method: ImportingConstructor]
public sealed class AkribisRightCouplingL(IConfigService configService, ILogger logger)
    : AkribisMotion<RightCouplingLConfig>(configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Right;
    public override AkribisMotionType AkribisMotionType => AkribisMotionType.LeftCoupling;
}

[Export]
[Export(typeof(IAkribisMotion))]
[Description("右工位_耦合右")]
[method: ImportingConstructor]
public sealed class AkribisRightCouplingR(IConfigService configService, ILogger logger)
    : AkribisMotion<RightCouplingRConfig>(configService, logger)
{
    public override WorkPos WorkPos => WorkPos.Right;
    public override AkribisMotionType AkribisMotionType => AkribisMotionType.RightCoupling;
}