using AFOCS.Infrastructure;

namespace AFOCS.Devices.AkribrisMotion;



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
