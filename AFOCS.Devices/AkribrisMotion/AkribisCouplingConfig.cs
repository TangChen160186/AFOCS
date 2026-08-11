using AFOCS.Infrastructure;

namespace AFOCS.Devices.AkribrisMotion;

public class AkribisCouplingConfig : ICloneable
{
    public string Ip { get; set; }
    public bool Ark { get; set; }
    public bool AutoReconnect { get; set; }

    public AkribisAxisParams XAxis { get; set; } = new();
    public AkribisAxisParams YAxis { get; set; } = new();
    public AkribisAxisParams ZAxis { get; set; } = new();

    public virtual AkribisCouplingConfig Clone() => new()
    {
        Ip = Ip,
        Ark = Ark,
        AutoReconnect = AutoReconnect,
        XAxis = XAxis.Clone(),
        YAxis = YAxis.Clone(),
        ZAxis = ZAxis.Clone(),
    };
    object ICloneable.Clone() => Clone();
}
 
/*
 * 备注:雅克贝斯轴，2048_00 == 1mm,如果要用mm作为运动单位，加速度、减速度、速度、传入值都要进行和2048_00换算
 */
public class AkribisAxisParams : ICloneable
{
    public int Speed { get; set; } = 2048_000; // 默认:10mm/s
    public int Accel { get; set; } = 2048_000;
    public int Decel { get; set; } = 2048_000;

    public AkribisAxisParams Clone() => new()
    {
        Speed = Speed,
        Accel = Accel,
        Decel = Decel,
    };
    object ICloneable.Clone() => Clone();
}


[ConfigPath("设备/雅克贝斯/左工位_耦合左")]
public class LeftCouplingLConfig : AkribisCouplingConfig
{
    public LeftCouplingLConfig() => Ip = "172.1.1.101";
    public override AkribisCouplingConfig Clone() => new LeftCouplingLConfig
    {
        Ip = Ip,
        Ark = Ark,
        AutoReconnect = AutoReconnect,
        XAxis = XAxis.Clone(),
        YAxis = YAxis.Clone(),
        ZAxis = ZAxis.Clone(),
    };
}

[ConfigPath("设备/雅克贝斯/左工位_耦合右")]
public class LeftCouplingRConfig : AkribisCouplingConfig
{
    public LeftCouplingRConfig() => Ip = "172.1.1.100";
    public override AkribisCouplingConfig Clone() => new LeftCouplingRConfig
    {
        Ip = Ip,
        Ark = Ark,
        AutoReconnect = AutoReconnect,
        XAxis = XAxis.Clone(),
        YAxis = YAxis.Clone(),
        ZAxis = ZAxis.Clone(),
    };
}
[ConfigPath("设备/雅克贝斯/右工位_耦合左")]
public class RightCouplingLConfig : AkribisCouplingConfig
{
    public RightCouplingLConfig() => Ip = "172.1.1.103";
    public override AkribisCouplingConfig Clone() => new RightCouplingLConfig
    {
        Ip = Ip,
        Ark = Ark,
        AutoReconnect = AutoReconnect,
        XAxis = XAxis.Clone(),
        YAxis = YAxis.Clone(),
        ZAxis = ZAxis.Clone(),
    };
}

[ConfigPath("设备/雅克贝斯/右工位_耦合右")]
public class RightCouplingRConfig : AkribisCouplingConfig
{
    public RightCouplingRConfig() => Ip = "172.1.1.102";
    public override AkribisCouplingConfig Clone() => new RightCouplingRConfig
    {
        Ip = Ip,
        Ark = Ark,
        AutoReconnect = AutoReconnect,
        XAxis = XAxis.Clone(),
        YAxis = YAxis.Clone(),
        ZAxis = ZAxis.Clone(),
    };
}
