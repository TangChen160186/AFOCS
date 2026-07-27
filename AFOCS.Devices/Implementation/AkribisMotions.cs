using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation;

/// <summary>左工位左耦合 — 雅克贝斯控制器</summary>
[Export]
[Export(typeof(IAkribisMotion))]
[method: ImportingConstructor]
public sealed class AkribisLeftCouplingL(IConfigService configService, ILogger logger)
    : AkribisMotion(configService, logger)
{
    protected override Type ConfigType => typeof(LeftCouplingLConfig);
}

/// <summary>左工位右耦合 — 雅克贝斯控制器</summary>
[Export]
[Export(typeof(IAkribisMotion))]
[method: ImportingConstructor]
public sealed class AkribisLeftCouplingR(IConfigService configService, ILogger logger)
    : AkribisMotion(configService, logger)
{
    protected override Type ConfigType => typeof(LeftCouplingRConfig);
}

/// <summary>右工位左耦合 — 雅克贝斯控制器</summary>
[Export]
[Export(typeof(IAkribisMotion))]
[method: ImportingConstructor]
public sealed class AkribisRightCouplingL(IConfigService configService, ILogger logger)
    : AkribisMotion(configService, logger)
{
    protected override Type ConfigType => typeof(RightCouplingLConfig);
}


/// <summary>右工位右耦合 — 雅克贝斯控制器</summary>
[Export]
[Export(typeof(IAkribisMotion))]
[method: ImportingConstructor]
public sealed class AkribisRightCouplingR(IConfigService configService, ILogger logger)
    : AkribisMotion(configService, logger)
{
    protected override Type ConfigType => typeof(RightCouplingRConfig);
}