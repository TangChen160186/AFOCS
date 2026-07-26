﻿﻿﻿using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation;

/// <summary>
/// SMC 电夹爪配置
/// </summary>
public class SmcGripperConfig : ICloneable
{
    public ushort SlaveAddress { get; set; }

    public SmcGripperConfig Clone() => new() { SlaveAddress = SlaveAddress };
    object ICloneable.Clone() => Clone();
}

// ============================================================
// 事件参数
// ============================================================

public class GripperDataChangedEventArgs(
    int currentPosition,
    bool isEnabled,
    bool isAlarm,
    ushort statusWord) : EventArgs
{
    /// <summary>当前位置 [0.01mm]</summary>
    public int CurrentPosition { get; } = currentPosition;
    /// <summary>使能状态</summary>
    public bool IsEnabled { get; } = isEnabled;
    /// <summary>报警状态</summary>
    public bool IsAlarm { get; } = isAlarm;
    /// <summary>状态字原始值</summary>
    public ushort StatusWord { get; } = statusWord;
    public DateTime Timestamp { get; } = DateTime.Now;
}

/// <summary>
/// SMC 电夹爪接口（纯位置模式，每个实例代表一个物理夹爪）
/// InitializeAsync 自动完成使能 + 回零 + 启动轮询
/// </summary>
public interface ISmcGripper : IDevice
{
    string DisplayName { get; }

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

/// <summary>
/// SMC 电夹爪基类 —— 每个实例代表一个物理夹爪，通过 EtherCAT PDO 控制
/// InitializeAsync 后自动启动后台轮询，通过 DataChanged 事件获取实时数据
/// </summary>
public abstract class SmcGripperBase(
    IMotionControlCard motionCard,
    IConfigService configService,
    ILogger logger) : ISmcGripper
{
    private SmcGripperConfig _config = new();

    public abstract string DisplayName { get; }
    protected abstract ushort DefaultSlaveAddress { get; }
    protected abstract Type ConfigType { get; }

    // OD 地址常量
    private const ushort OdStatus = 0x6010;
    private const ushort OdCurrentPosition = 0x6020;
    private const ushort OdControlWord = 0x7010;
    private const ushort OdControl = 0x7012;
    private const ushort OdSpeed = 0x7021;
    private const ushort OdPosition = 0x7022;
    private const ushort OdForceUpper = 0x7025;
    private const ushort OdForceLower = 0x7026;
    private const ushort OdThrustDist = 0x702B;
    private const ushort OdSubIndex = 0x00;

    private const ushort OdBitLen8 = 8;
    private const ushort OdBitLen16 = 16;
    private const ushort OdBitLen32 = 32;

    private const ushort InpBit = 11; // INP (定位完成)

    // 后台轮询
    private CancellationTokenSource? _cts;
    private readonly Lock _lock = new();

    // 缓存值
    private int _position;
    private bool _isEnabled;
    private bool _isAlarm;

    public bool IsConnected => motionCard.IsConnected;
    public bool IsMonitoring { get; private set; }
    public event EventHandler<GripperDataChangedEventArgs>? DataChanged;

    // 只读缓存属性
    public int CurrentPosition { get { lock (_lock) return _position; } }
    public bool IsEnabledCached { get { lock (_lock) return _isEnabled; } }
    public bool IsAlarmCached { get { lock (_lock) return _isAlarm; } }

    // ====================================================================
    // IDevice
    // ====================================================================

    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        var loaded = await configService.LoadAsync(ConfigType);
        if (loaded is SmcGripperConfig config)
            _config = config;
        else
        {
            _config = new SmcGripperConfig { SlaveAddress = DefaultSlaveAddress };
            await configService.SaveAsync(ConfigType, _config);
        }

        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接，夹爪无法初始化");

        // 步骤1: 使能
        var enableResult = await EnableAsync();
        if (!enableResult.IsSuccess)
            return Result.Fail($"{DisplayName} 使能失败: {enableResult.Message}");

        // 步骤2: 回零
        var homeResult = await HomeAsync();
        if (!homeResult.IsSuccess)
        {
            logger.Warning("{Name} 回零失败: {Msg}", DisplayName, homeResult.Message);
            // 回零失败不阻止初始化，只告警
        }

        // 步骤3: 启动后台轮询
        StartMonitoring();

        logger.Information("{Name} 初始化完成，从站={Addr}，轮询已启动", DisplayName, _config.SlaveAddress);
        return Result.Success();
    }

    public Task<Result> StopAsync(CancellationToken token = default)
    {
        StopMonitoring();
        return Task.FromResult(Result.Success());
    }

    public Task<Result> ReConnectAsync(CancellationToken token = default)
        => InitializeAsync(token);

    // ====================================================================
    // 后台轮询
    // ====================================================================

    public void StartMonitoring(int intervalMs = 100)
    {
        if (IsMonitoring) return;
        _cts = new CancellationTokenSource();
        IsMonitoring = true;
        logger.Information("{Name} 后台轮询已启动，间隔 {Interval}ms", DisplayName, intervalMs);
        _ = Task.Run(() => PollLoopAsync(intervalMs, _cts.Token), _cts.Token);
    }

    public void StopMonitoring()
    {
        if (!IsMonitoring) return;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        IsMonitoring = false;
        logger.Information("{Name} 后台轮询已停止", DisplayName);
    }

    private async Task PollLoopAsync(int intervalMs, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(intervalMs, ct);

                if (!motionCard.IsConnected) continue;

                // 并发读取状态字和当前位置
                var statusTask = motionCard.ReadTxPDOAsync(_config.SlaveAddress, OdStatus, OdSubIndex, OdBitLen16);
                var posTask = motionCard.ReadTxPDOAsync(_config.SlaveAddress, OdCurrentPosition, OdSubIndex, OdBitLen32);

                await Task.WhenAll(statusTask, posTask);

                if (!statusTask.Result.IsSuccess || !posTask.Result.IsSuccess)
                    continue;

                ushort statusWord = (ushort)statusTask.Result.Data;
                int position = posTask.Result.Data;
                bool enabled = (statusWord & (1 << 9)) != 0;
                bool alarm = (statusWord & (1 << 15)) != 0;

                bool changed;
                lock (_lock)
                {
                    changed = _position != position || _isEnabled != enabled || _isAlarm != alarm;
                    if (changed)
                    {
                        _position = position;
                        _isEnabled = enabled;
                        _isAlarm = alarm;
                    }
                }

                if (changed)
                {
                    DataChanged?.Invoke(this, new GripperDataChangedEventArgs(position, enabled, alarm, statusWord));
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.Error(ex, "{Name} 轮询异常", DisplayName);
            }
        }
    }

    // ====================================================================
    // 公开方法
    // ====================================================================

    public async Task<Result> EnableAsync()
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        var result = await motionCard.WriteRxPDOAsync(_config.SlaveAddress, OdControlWord, OdSubIndex, OdBitLen16, 512);
        if (!result.IsSuccess)
            return Result.Fail($"{DisplayName} 使能失败: {result.Message}");

        logger.Information("{Name} 使能完成", DisplayName);
        return Result.Success();
    }

    public async Task<Result<bool>> IsEnabledAsync()
    {
        var statusResult = await GetStatusAsync();
        if (!statusResult.IsSuccess)
            return Result<bool>.Fail(statusResult.Message);

        return Result<bool>.Success((statusResult.Data & (1 << 9)) != 0);
    }

    public async Task<Result<bool>> IsAlarmAsync()
    {
        var statusResult = await GetStatusAsync();
        if (!statusResult.IsSuccess)
            return Result<bool>.Fail(statusResult.Message);

        return Result<bool>.Success((statusResult.Data & (1 << 15)) != 0);
    }

    public async Task<Result<ushort>> GetStatusAsync()
    {
        if (!motionCard.IsConnected)
            return Result<ushort>.Fail("运动控制卡未连接");

        var result = await motionCard.ReadTxPDOAsync(_config.SlaveAddress, OdStatus, OdSubIndex, OdBitLen16);
        if (!result.IsSuccess)
            return Result<ushort>.Fail(result.Message);

        return Result<ushort>.Success((ushort)result.Data);
    }

    public async Task<Result<int>> GetPositionAsync()
    {
        if (!motionCard.IsConnected)
            return Result<int>.Fail("运动控制卡未连接");

        var result = await motionCard.ReadTxPDOAsync(_config.SlaveAddress, OdCurrentPosition, OdSubIndex, OdBitLen32);
        if (!result.IsSuccess)
            return Result<int>.Fail(result.Message);

        return Result<int>.Success(result.Data);
    }

    public async Task<Result> HomeAsync(int timeoutMs = 10000)
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        // 检查使能
        var enabledResult = await IsEnabledAsync();
        if (!enabledResult.IsSuccess || !enabledResult.Data)
            return Result.Fail($"{DisplayName} 未使能");

        logger.Information("{Name} 开始回零...", DisplayName);

        try
        {
            // 写 0x7010 = 4608 (512 | 4096)
            var writeResult = await motionCard.WriteRxPDOAsync(_config.SlaveAddress, OdControlWord, OdSubIndex, OdBitLen16, 4608);
            if (!writeResult.IsSuccess)
                return Result.Fail($"{DisplayName} 回零启动失败: {writeResult.Message}");

            // 等 bit10 = ON
            var waitResult = await WaitForStatusBitAsync(10, true, timeoutMs);
            if (!waitResult.IsSuccess)
                return Result.Fail($"{DisplayName} 回零失败: {waitResult.Message}");

            // 恢复 512（关闭回零，保持使能）
            await motionCard.WriteRxPDOAsync(_config.SlaveAddress, OdControlWord, OdSubIndex, OdBitLen16, 512);

            logger.Information("{Name} 回零完成", DisplayName);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "{Name} 回零异常", DisplayName);
            return Result.Fail($"回零异常: {ex.Message}");
        }
    }

    public async Task<Result> AlarmResetAsync()
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        try
        {
            // 置位 bit11: 512 | 2048 = 2560
            await motionCard.WriteRxPDOAsync(_config.SlaveAddress, OdControlWord, OdSubIndex, OdBitLen16, 2560);
            await Task.Delay(50);
            // 恢复 512
            await motionCard.WriteRxPDOAsync(_config.SlaveAddress, OdControlWord, OdSubIndex, OdBitLen16, 512);

            logger.Information("{Name} 报警复位完成", DisplayName);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "{Name} 报警复位异常", DisplayName);
            return Result.Fail($"报警复位异常: {ex.Message}");
        }
    }

    public async Task<Result> MoveAsync(ushort speed, ushort position, int timeoutMs = 5000)
    {
        var setSpeed = await SetParameterAsync(OdSpeed, speed, "速度");
        if (!setSpeed.IsSuccess) return setSpeed;

        var setPos = await SetParameterAsync(OdPosition, position, "动作位置", OdBitLen32);
        if (!setPos.IsSuccess) return setPos;

        var setForceUp = await SetParameterAsync(OdForceUpper, 0, "推力上限");
        if (!setForceUp.IsSuccess) return setForceUp;

        var setForceLow = await SetParameterAsync(OdForceLower, 0, "推力下限");
        if (!setForceLow.IsSuccess) return setForceLow;

        var setDist = await SetParameterAsync(OdThrustDist, 50, "推力距离", OdBitLen32);
        if (!setDist.IsSuccess) return setDist;

        return await StartAsync(timeoutMs);
    }

    // ====================================================================
    // 配置读写
    // ====================================================================

    public SmcGripperConfig GetConfig() => _config.Clone();

    public async Task SaveConfigAsync(SmcGripperConfig config)
    {
        var cloned = config.Clone();
        lock (_lock)
        {
            _config = cloned;
        }
        await configService.SaveAsync(ConfigType, _config);
        logger.Information("{Name} 配置已保存", DisplayName);
    }

    // ====================================================================
    // 内部方法
    // ====================================================================

    private async Task<Result> StartAsync(int timeoutMs = 15000)
    {
        logger.Information("{Name} (从站{Addr}) 开始定位...", DisplayName, _config.SlaveAddress);

        try
        {
            // 启动: 写 0x7012:00 = 1
            var writeResult = await motionCard.WriteRxPDOAsync(_config.SlaveAddress, OdControl, OdSubIndex, OdBitLen8, 1);
            if (!writeResult.IsSuccess)
                return Result.Fail($"{DisplayName} 启动失败: {writeResult.Message}");

            // 等 INP(bit11) = ON
            var waitResult = await WaitForStatusBitAsync(InpBit, true, timeoutMs);
            if (!waitResult.IsSuccess)
                return Result.Fail($"{DisplayName} 定位失败: {waitResult.Message}");

            await Task.Delay(50);

            // 复位: 写 0x7012:00 = 0
            await motionCard.WriteRxPDOAsync(_config.SlaveAddress, OdControl, OdSubIndex, OdBitLen8, 0);

            logger.Information("{Name} 定位完成", DisplayName);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "{Name} 定位异常", DisplayName);
            return Result.Fail($"定位异常: {ex.Message}");
        }
    }

    private async Task<Result> WaitForStatusBitAsync(ushort bit, bool expectedValue, int timeoutMs = 3000)
    {
        int elapsed = 0;
        const int pollInterval = 20;

        while (elapsed < timeoutMs)
        {
            var statusResult = await motionCard.ReadTxPDOAsync(_config.SlaveAddress, OdStatus, OdSubIndex, OdBitLen16);
            if (statusResult.IsSuccess)
            {
                ushort status = (ushort)statusResult.Data;
                bool bitValue = (status & (1 << bit)) != 0;

                if (bitValue == expectedValue)
                {
                    logger.Debug("{Name} bit{bit}={Value}, 0x6010=0x{Status:X4}", DisplayName, bit, bitValue, status);
                    return Result.Success();
                }
            }

            await Task.Delay(pollInterval);
            elapsed += pollInterval;
        }

        return Result.Fail($"{DisplayName} bit{bit} 等待 {expectedValue} 超时 ({timeoutMs}ms)");
    }

    private async Task<Result> SetParameterAsync(ushort odIndex, ushort value, string paramName, ushort dataLength = OdBitLen16)
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        var result = await motionCard.WriteRxPDOAsync(_config.SlaveAddress, odIndex, OdSubIndex, dataLength, value);
        if (!result.IsSuccess)
            return Result.Fail($"{DisplayName} 设置{paramName}={value}失败: {result.Message}");

        logger.Debug("{Name} 设置{Param}={Value}", DisplayName, paramName, value);
        return Result.Success();
    }

    public void Dispose()
    {
        StopMonitoring();
        GC.SuppressFinalize(this);
    }
}
