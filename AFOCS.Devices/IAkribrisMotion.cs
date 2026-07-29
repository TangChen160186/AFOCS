using AFOCS.Infrastructure;

namespace AFOCS.Devices;

// ========== 轴参数 ==========

public class AkribisAxisParams : ICloneable
{
    public int Speed { get; set; } = 1000;
    public int Accel { get; set; } = 400;
    public int Decel { get; set; } = 400;

    public AkribisAxisParams Clone() => new()
    {
        Speed = Speed,
        Accel = Accel,
        Decel = Decel,
    };
    object ICloneable.Clone() => Clone();
}

// ========== 耦合工位配置 ==========

public class AkribisCouplingConfig : ICloneable
{
    public string Ip { get; set; } = "172.1.1.101";
    public bool Ark { get; set; } = false;
    public bool AutoReconnect { get; set; } = false;

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

// ========== 4 个工位专用配置 ==========

public class LeftCouplingLConfig : AkribisCouplingConfig
{
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

public class LeftCouplingRConfig : AkribisCouplingConfig
{
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

public class RightCouplingLConfig : AkribisCouplingConfig
{
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

public class RightCouplingRConfig : AkribisCouplingConfig
{
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

// ========== 枚举 ==========

public enum AkribisAxisId
{
    X,
    Y,
    Z
}

// ========== 接口 ==========

/// <summary>雅克贝斯轴位置变化事件参数</summary>
public class AkribisPositionChangedEventArgs(int x, int y, int z) : EventArgs
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;
    public DateTime Timestamp { get; } = DateTime.Now;
}

public interface IAkribisMotion : IDevice
{
    /// <summary>位置变化事件（后台轮询触发，~100ms 间隔）</summary>
    event EventHandler<AkribisPositionChangedEventArgs>? PositionChanged;

    /// <summary>是否正在轮询</summary>
    bool IsMonitoring { get; }

    /// <summary>X 轴当前位置（缓存）</summary>
    int PositionX { get; }
    /// <summary>Y 轴当前位置（缓存）</summary>
    int PositionY { get; }
    /// <summary>Z 轴当前位置（缓存）</summary>
    int PositionZ { get; }

    /// <summary>获取配置（返回 Clone，外部修改不影响内部状态）</summary>
    AkribisCouplingConfig GetConfig();

    /// <summary>用新配置覆盖并持久化</summary>
    Task SaveConfigAsync(AkribisCouplingConfig config);

    /// <summary>获取轴参数（从当前配置读取）</summary>
    AkribisAxisParams GetAxisParams(AkribisAxisId axis);

    // ---- 轴控制 ----

    Task<Result> HomeAsync(AkribisAxisId axis,int timeoutMs = 0);
    Task<Result> EnableAsync(AkribisAxisId axis);
    Task<Result> DisEnableAsync(AkribisAxisId axis);
    Task<Result> MoveRelativeAsync(AkribisAxisId axis, int distance, int? speed = null, int? accel = null, int? decel = null, int timeoutMs = 0);
    Task<Result> MoveAbsAsync(AkribisAxisId axis, int position, int? speed = null, int? accel = null, int? decel = null, int timeoutMs = 0);
    Task<Result> MoveLineRelativeAsync(AkribisAxisId[] axiss, int[] distances, int? speed = null, int? accel = null, int? decel = null, int timeoutMs = 0);
    Task<Result> StopAxisAsync(AkribisAxisId axis);
    Task<Result> StopAxisAsync();
    Task<Result> EmergencyStopAsync(AkribisAxisId axis);
    Task<Result> EmergencyStopAllAsync();
}
