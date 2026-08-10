using AFOCS.Infrastructure;

namespace AFOCS.Devices.AkribrisMotion;


public class AkribisPositionChangedEventArgs(int x, int y, int z) : EventArgs
{
    public int X { get; } = x; // 脉冲值
    public int Y { get; } = y;
    public int Z { get; } = z;
}

public interface IAkribisMotion : IDevice
{
    AkribisMotionType AkribisMotionType { get; }
    event EventHandler<AkribisPositionChangedEventArgs>? PositionChanged;
    bool IsMonitoring { get; }
    int PositionX { get; }
    int PositionY { get; }
    int PositionZ { get; }
    AkribisCouplingConfig GetConfig();
    Task SaveConfigAsync(AkribisCouplingConfig config);

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
