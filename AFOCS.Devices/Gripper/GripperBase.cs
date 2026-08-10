using AFOCS.Devices.MotionControlCard;
using AFOCS.Devices.PressureSensor;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Gripper;


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
/// SMC 电夹爪基类 —— 每个实例代表一个物理夹爪，通过 EtherCAT PDO 控制
/// InitializeAsync 后自动启动后台轮询，通过 DataChanged 事件获取实时数据
/// </summary>
public abstract class GripperBase<TConfig>(
    IMotionControlCard motionCard,
    IConfigService configService,
    ILogger logger) : IGripper 
    where TConfig : GripperConfig
{

    private GripperConfig _config = new();
    // OD 地址常量
    private const ushort OdPushForce = 0x7011;  // RxPDO 推理推力
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
    public abstract WorkPos WorkPos { get; }
    public abstract GripperType GripperType { get; }
    public bool IsMonitoring { get; private set; }
    public event EventHandler<GripperDataChangedEventArgs>? DataChanged;


    public int CurrentPosition { get { lock (_lock) return _position; } }
    public bool IsEnabledCached { get { lock (_lock) return _isEnabled; } }
    public bool IsAlarmCached { get { lock (_lock) return _isAlarm; } }

    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        var loaded = await configService.LoadAsync(typeof(TConfig));
        if (loaded is GripperConfig config)
            _config = config;
        else
        {
            _config = (GripperConfig)Activator.CreateInstance(typeof(TConfig))!;
            await configService.SaveAsync(typeof(TConfig), _config);
        }

        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接，夹爪无法初始化");
        await EnablePushForceAsync();

        // 步骤1: 使能
        var enableResult = await EnableAsync();
        if (!enableResult.IsSuccess)
            return Result.Fail($"{GetType()} 使能失败: {enableResult.Message}");

        // 步骤2: 回零
        var homeResult = await HomeAsync();
        if (!homeResult.IsSuccess)
        {
            logger.Warning("{Name} 回零失败: {Msg}", GetType(), homeResult.Message);
            // 回零失败不阻止初始化，只告警
        }

        // 步骤3: 启动后台轮询
        StartMonitoring();

        logger.Information("{Name} 初始化完成，从站={Addr}，轮询已启动", GetType(), _config.SlaveAddress);
        return Result.Success();
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
        logger.Information("{Name} 后台轮询已启动，间隔 {Interval}ms", GetType(), intervalMs);
        _ = Task.Run(() => PollLoopAsync(intervalMs, _cts.Token), _cts.Token);
    }

    public void StopMonitoring()
    {
        if (!IsMonitoring) return;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        IsMonitoring = false;
        logger.Information("{Name} 后台轮询已停止", GetType());
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

                lock (_lock)
                {
                    _position = position;
                    _isEnabled = enabled;
                    _isAlarm = alarm;
                    DataChanged?.Invoke(this, new GripperDataChangedEventArgs(position, enabled, alarm, statusWord));
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.Error(ex, "{Name} 轮询异常", GetType());
            }
        }
    }

    /// <summary>
    /// 开启推理推力模式（写 0x7011:00 = 34400）
    /// </summary>
    public async Task<Result> EnablePushForceAsync()
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");


        var result = await motionCard.WriteRxPDOAsync(_config.SlaveAddress, OdPushForce, OdSubIndex, OdBitLen16, 34400);
        if (!result.IsSuccess)
            return Result.Fail($"夹爪  开启推理推力模式失败: {result.Message}");

        logger.Information("夹爪 推理推力模式已开启");
        return Result.Success();
    }
    public async Task<Result> EnableAsync()
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        var result = await motionCard.WriteRxPDOAsync(_config.SlaveAddress, OdControlWord, OdSubIndex, OdBitLen16, 512);
        if (!result.IsSuccess)
            return Result.Fail($"{GetType()} 使能失败: {result.Message}");

        logger.Information("{Name} 使能完成", GetType());
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
            return Result.Fail($"{GetType()} 未使能");

        logger.Information("{Name} 开始回零...", GetType());

        try
        {
            // 写 0x7010 = 4608 (512 | 4096)
            var writeResult = await motionCard.WriteRxPDOAsync(_config.SlaveAddress, OdControlWord, OdSubIndex, OdBitLen16, 4608);
            if (!writeResult.IsSuccess)
                return Result.Fail($"{GetType()} 回零启动失败: {writeResult.Message}");

            // 等 bit10 = ON
            var waitResult = await WaitForStatusBitAsync(10, true, timeoutMs);
            if (!waitResult.IsSuccess)
                return Result.Fail($"{GetType()} 回零失败: {waitResult.Message}");

            // 恢复 512（关闭回零，保持使能）
            await motionCard.WriteRxPDOAsync(_config.SlaveAddress, OdControlWord, OdSubIndex, OdBitLen16, 512);

            logger.Information("{Name} 回零完成", GetType());
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "{Name} 回零异常", GetType());
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

            logger.Information("{Name} 报警复位完成", GetType());
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "{Name} 报警复位异常", GetType());
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



    public GripperConfig GetConfig() => _config.Clone();

    public async Task SaveConfigAsync(GripperConfig config)
    {
        var cloned = config.Clone();
        lock (_lock)
        {
            _config = cloned;
        }
        await configService.SaveAsync(typeof(TConfig), _config);
        logger.Information("{Name} 配置已保存", GetType());
    }


    private async Task<Result> StartAsync(int timeoutMs = 15000)
    {
        logger.Information("{Name} (从站{Addr}) 开始定位...", GetType(), _config.SlaveAddress);

        try
        {
            // 启动: 写 0x7012:00 = 1
            var writeResult = await motionCard.WriteRxPDOAsync(_config.SlaveAddress, OdControl, OdSubIndex, OdBitLen8, 1);
            if (!writeResult.IsSuccess)
                return Result.Fail($"{GetType()} 启动失败: {writeResult.Message}");

            // 等 INP(bit11) = ON
            var waitResult = await WaitForStatusBitAsync(InpBit, true, timeoutMs);
            if (!waitResult.IsSuccess)
                return Result.Fail($"{GetType()} 定位失败: {waitResult.Message}");

            await Task.Delay(50);

            // 复位: 写 0x7012:00 = 0
            await motionCard.WriteRxPDOAsync(_config.SlaveAddress, OdControl, OdSubIndex, OdBitLen8, 0);

            logger.Information("{Name} 定位完成", GetType());
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "{Name} 定位异常", GetType());
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
                    logger.Debug("{Name} bit{bit}={Value}, 0x6010=0x{Status:X4}", GetType(), bit, bitValue, status);
                    return Result.Success();
                }
            }

            await Task.Delay(pollInterval);
            elapsed += pollInterval;
        }

        return Result.Fail($"{GetType()} bit{bit} 等待 {expectedValue} 超时 ({timeoutMs}ms)");
    }

    private async Task<Result> SetParameterAsync(ushort odIndex, ushort value, string paramName, ushort dataLength = OdBitLen16)
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        var result = await motionCard.WriteRxPDOAsync(_config.SlaveAddress, odIndex, OdSubIndex, dataLength, value);
        if (!result.IsSuccess)
            return Result.Fail($"{GetType()} 设置{paramName}={value}失败: {result.Message}");

        logger.Debug("{Name} 设置{Param}={Value}", GetType(), paramName, value);
        return Result.Success();
    }

    public void Dispose()
    {
        StopMonitoring();
        GC.SuppressFinalize(this);
    }
}
