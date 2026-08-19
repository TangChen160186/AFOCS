using AFOCS.Infrastructure;

namespace AFOCS.Devices.AkribrisMotion;

public interface IAkribisMotion : IDevice
{
    WorkPos WorkPos { get; }
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

    // ---- 耦合找光 ----
    Task<Result<AkribisCouplingResult>> SingleAxisCouplingAsync(SingleAxisCouplingArgs args, CancellationToken token = default);
    Task<Result<AkribisCouplingResult>> SpiralCouplingAsync(SpiralCouplingArgs args, CancellationToken token = default);
}

public class AkribisPositionChangedEventArgs(int x, int y, int z) : EventArgs
{
    public int X { get; } = x; // 脉冲值
    public int Y { get; } = y;
    public int Z { get; } = z;
}

/// <summary>耦合找光返回结果</summary>
public class AkribisCouplingResult
{
    /// <summary>各采集通道光功率数据序列（单轴 CH1~4 各最多 3000 点，螺旋 CH1~4 各最多 2500 点）</summary>
    public Dictionary<int, List<double>>? ChannelPower { get; set; }

    /// <summary>单轴扫描各采样点轴位置坐标（脉冲，来源 AGenData[1000-3999]，与 ChannelPower 各通道等长对齐）</summary>
    public List<double> AxisPositions { get; set; } = [];

    /// <summary>各通道光功率峰值对应位置坐标（脉冲，通道号 → 位置），来源 AGenData[704-707]</summary>
    public Dictionary<int, double> PeakPositions { get; set; } = [];

    /// <summary>角度（度），仅单轴耦合有效，来源 AGenData[817]/1000</summary>
    public double Angle { get; set; }

    /// <summary>耦合结果码（AGenData[602]）：100=成功并回归最大光功率位置，600=成功并回归交点位置，700=成功并回归最优点位置，-100=中断信号导致失败，-200=错误导致失败（详见 SuccessCode）</summary>
    public int CouplingResult { get; set; }

    /// <summary>报错代码（AGenData[650]，仅 CouplingResult=-200 时有意义），可通过 GetCouplingErrorMessage 转为可读信息</summary>
    public int SuccessCode { get; set; }

    /// <summary>报错码（AGenData[602]，仅螺旋耦合有效）</summary>
    public int ErrorCode { get; set; }
}

/// <summary>单轴耦合（单轴找光）参数</summary>
public class SingleAxisCouplingArgs
{
    /// <summary>运动轴：0=A, 1=B, 2=C</summary>
    public int Axis { get; set; }

    /// <summary>采样间距（脉冲）</summary>
    public double SamplingInterval { get; set; } = 10;

    /// <summary>起始距离（相对当前位置，脉冲）</summary>
    public double StartDistance { get; set; } = -1024;

    /// <summary>停止距离（相对当前位置，脉冲）</summary>
    public double StopDistance { get; set; } = 1024;

    /// <summary>最大扫描速度（脉冲/s）</summary>
    public double MaxScanSpeed { get; set; } = 204800;

    /// <summary>最大回归速度（脉冲/s）</summary>
    public double MaxReturnSpeed { get; set; } = 20480;

    /// <summary>间距宽度（um），内部按 20um=4096 脉冲换算</summary>
    public double SpacingWidthUm { get; set; } = 20;

    /// <summary>采集通道（十进制，按二进制位定义）</summary>
    public int AcquireChannel { get; set; } = 1;
}

/// <summary>螺旋耦合（螺旋找光）参数</summary>
public class SpiralCouplingArgs
{
    /// <summary>1# 运动轴：0=A, 1=B, 2=C</summary>
    public int Axis1 { get; set; }

    /// <summary>2# 运动轴：0=A, 1=B, 2=C</summary>
    public int Axis2 { get; set; } = 1;

    /// <summary>螺距</summary>
    public double Pitch { get; set; } = 1.0;

    /// <summary>最大扫描半径（脉冲）</summary>
    public double MaxScanRadius { get; set; } = 500;

    /// <summary>最大扫描速度（脉冲/s）</summary>
    public double MaxScanSpeed { get; set; } = 204800;

    /// <summary>最大回归速度（脉冲/s）</summary>
    public double MaxReturnSpeed { get; set; } = 20480;

    /// <summary>采集通道（十进制，按二进制位定义）</summary>
    public int AcquireChannel { get; set; } = 1;
}