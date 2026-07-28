using AFOCS.Devices.Implementation;
using AFOCS.Infrastructure;

namespace AFOCS.Devices;

/// <summary>
/// SMC 电夹爪接口（纯位置模式，每个实例代表一个物理夹爪）
/// InitializeAsync 自动完成使能 + 回零 + 启动轮询
/// </summary>
public interface ISmcGripper : IDevice
{
    string DisplayName { get; }

    /// <summary>当前位置缓存值 [0.01mm]</summary>
    int CurrentPosition { get; }
    /// <summary>使能状态缓存</summary>
    bool IsEnabledCached { get; }
    /// <summary>报警状态缓存</summary>
    bool IsAlarmCached { get; }

    /// <summary>数据变化事件（轮询检测到位置/状态变化时触发）</summary>
    event EventHandler<GripperDataChangedEventArgs>? DataChanged;

    /// <summary>是否正在监控</summary>
    bool IsMonitoring { get; }

    /// <summary>使能（写 0x7010:00 = 512）</summary>
    Task<Result> EnableAsync();

    /// <summary>回零（使能 → 写 4608 → 等 bit10=ON → 恢复 512）</summary>
    Task<Result> HomeAsync(int timeoutMs = 10000);

    /// <summary>报警复位</summary>
    Task<Result> AlarmResetAsync();

    /// <summary>检查使能状态（0x6010 bit9）</summary>
    Task<Result<bool>> IsEnabledAsync();

    /// <summary>检查报警状态（0x6010 bit15）</summary>
    Task<Result<bool>> IsAlarmAsync();

    /// <summary>获取状态字（0x6010）</summary>
    Task<Result<ushort>> GetStatusAsync();

    /// <summary>获取当前位置（0x6020, 单位 0.01mm）</summary>
    Task<Result<int>> GetPositionAsync();

    /// <summary>夹爪动作（设置速度+位置，推力清零，推力距离=50，执行定位）</summary>
    Task<Result> MoveAsync(ushort speed, ushort position, int timeoutMs = 5000);

    /// <summary>获取当前配置</summary>
    SmcGripperConfig GetConfig();

    /// <summary>保存配置到文件</summary>
    Task SaveConfigAsync(SmcGripperConfig config);
}