using AFOCS.Infrastructure;

namespace AFOCS.Devices;

/// <summary>
/// 压力传感器配置（每个传感器实例独立一份）
/// </summary>
public class PressureSensorConfig : ICloneable
{
    /// <summary>从站地址</summary>
    public ushort SlaveAddress { get; set; }

    /// <summary>
    /// 通道 → OD 子索引映射
    /// 默认：X→1, Y→2, Z→3
    /// 如果硬件接线不同，可在此调整（比如 X 接了通道2，则 X→2）
    /// </summary>
    public Dictionary<PressureChannel, ushort> ChannelSubIndexMapping { get; set; } = new()
    {
        [PressureChannel.X] = 1,
        [PressureChannel.Y] = 2,
        [PressureChannel.Z] = 3,
    };

    /// <summary>
    /// 报警阈值（通道 → 压力值），0 表示禁用该通道报警
    /// </summary>
    public Dictionary<PressureChannel, int> AlarmThresholds { get; set; } = new()
    {
        [PressureChannel.X] = 400,
        [PressureChannel.Y] = 400,
        [PressureChannel.Z] = 400,
    };

    /// <summary>获取指定通道的 OD 子索引</summary>
    public ushort GetSubIndex(PressureChannel channel) =>
        ChannelSubIndexMapping.TryGetValue(channel, out var idx) ? idx : (ushort)((int)channel + 1);

    /// <summary>获取指定通道的报警阈值（0 表示禁用）</summary>
    public int GetAlarmThreshold(PressureChannel channel) =>
        AlarmThresholds.GetValueOrDefault(channel, 0);

    /// <summary>深拷贝</summary>
    public PressureSensorConfig Clone() => new()
    {
        SlaveAddress = SlaveAddress,
        ChannelSubIndexMapping = new Dictionary<PressureChannel, ushort>(ChannelSubIndexMapping),
        AlarmThresholds = new Dictionary<PressureChannel, int>(AlarmThresholds),
    };

    object ICloneable.Clone() => Clone();
}

/// <summary>
/// 压力数据变化事件参数
/// </summary>
public class PressureDataChangedEventArgs : EventArgs
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    public DateTime Timestamp { get; }

    public PressureDataChangedEventArgs(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 压力报警事件参数
/// </summary>
public class PressureAlarmEventArgs : EventArgs
{
    /// <summary>触发报警的通道</summary>
    public PressureChannel Channel { get; }
    /// <summary>当前压力值</summary>
    public int CurrentValue { get; }
    /// <summary>报警阈值</summary>
    public int Threshold { get; }
    /// <summary>是否报警激活（true=超过阈值, false=恢复到阈值以下）</summary>
    public bool IsActive { get; }
    public DateTime Timestamp { get; }

    public PressureAlarmEventArgs(PressureChannel channel, int currentValue, int threshold, bool isActive)
    {
        Channel = channel;
        CurrentValue = currentValue;
        Threshold = threshold;
        IsActive = isActive;
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 压力传感器接口 —— 每个实例代表一个物理传感器（含 X/Y/Z 三通道）
/// 初始化后自动启动后台轮询，可通过事件订阅或 Get 方法获取最新值
/// </summary>
public interface IPressureSensor : IDevice
{
    /// <summary>传感器显示名称</summary>
    string DisplayName { get; }

    /// <summary>压力数据变化事件（轮询检测到任一通道值变化时触发）</summary>
    event EventHandler<PressureDataChangedEventArgs>? DataChanged;

    /// <summary>
    /// 压力报警事件（通道值超过/恢复阈值时触发）
    /// IsActive=true 表示报警激活，IsActive=false 表示报警解除
    /// </summary>
    event EventHandler<PressureAlarmEventArgs>? AlarmTriggered;

    /// <summary>启动后台轮询监控</summary>
    Task StartMonitoring(int intervalMs = 100);

    /// <summary>停止后台轮询</summary>
    void StopMonitoring();

    /// <summary>是否正在监控</summary>
    bool IsMonitoring { get; }

    // ========== 获取缓存值（非阻塞，直接返回最新轮询结果） ==========

    /// <summary>获取 X 通道最新缓存值</summary>
    int GetX();

    /// <summary>获取 Y 通道最新缓存值</summary>
    int GetY();

    /// <summary>获取 Z 通道最新缓存值</summary>
    int GetZ();

    // ========== 按需读取（主动发起一次 PDO 读） ==========

    /// <summary>读取 X 通道当前压力值</summary>
    Task<Result<int>> ReadXAsync();

    /// <summary>读取 Y 通道当前压力值</summary>
    Task<Result<int>> ReadYAsync();

    /// <summary>读取 Z 通道当前压力值</summary>
    Task<Result<int>> ReadZAsync();

    /// <summary>一次性读取全部三通道</summary>
    Task<Result<(int X, int Y, int Z)>> ReadAllAsync();

    // ========== 清零校准 ==========

    /// <summary>X 通道清零</summary>
    Task<Result> ZeroXAsync();

    /// <summary>Y 通道清零</summary>
    Task<Result> ZeroYAsync();

    /// <summary>Z 通道清零</summary>
    Task<Result> ZeroZAsync();

    /// <summary>全部三通道清零</summary>
    Task<Result> ZeroAllAsync();

    // ========== 配置读写 ==========

    /// <summary>获取当前配置</summary>
    PressureSensorConfig GetConfig();

    /// <summary>保存配置到文件</summary>
    Task SaveConfigAsync(PressureSensorConfig config);
}
