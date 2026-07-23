using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation;

/// <summary>
/// 压力传感器基类 —— 每个实例代表一个物理传感器
/// TConfig 用于隔离每个传感器的配置文件
/// </summary>
public abstract class PressureSensor : IPressureSensor
{
    private readonly IMotionControlCard _motionCard;
    private readonly IConfigService _configService;
    protected readonly ILogger Logger;

    private PressureSensorConfig _config = new();
    private CancellationTokenSource? _cts;
    private readonly Lock _lock = new();

    // 缓存最新值
    private int _x, _y, _z;

    // 报警状态（用于边沿检测）
    private bool _alarmX, _alarmY, _alarmZ;

    // OD 地址常量
    private const ushort OdReadPressure = 0x6000;
    private const ushort OdZeroControl = 0x7000;
    private const ushort OdSubIndex = 0x00;
    private const ushort OdBitLen32 = 32;

    // ---- 子类需覆写 ----

    /// <summary>传感器显示名称</summary>
    public abstract string DisplayName { get; }

    /// <summary>默认从站地址（配置不存在时使用）</summary>
    protected abstract ushort DefaultSlaveAddress { get; }

    /// <summary>配置文件类型（用于 ConfigService 存取隔离）</summary>
    protected abstract Type ConfigType { get; }

    public bool IsConnected => _motionCard.IsConnected;
    public bool IsMonitoring { get; private set; }
    public event EventHandler<PressureDataChangedEventArgs>? DataChanged;
    public event EventHandler<PressureAlarmEventArgs>? AlarmTriggered;

    protected PressureSensor(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    {
        _motionCard = motionCard;
        _configService = configService;
        Logger = logger;
    }

    // ====================================================================
    // IDevice
    // ====================================================================

    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        var loaded = await _configService.LoadAsync(ConfigType);
        if (loaded is PressureSensorConfig config)
            _config = config;
        else
        {
            _config = new PressureSensorConfig { SlaveAddress = DefaultSlaveAddress };
            await _configService.SaveAsync(ConfigType, _config);
        }

        if (!_motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接，压力传感器无法初始化");

        Logger.Information("[{Type}] 初始化完成，从站地址={Addr}, 通道映射 X→{X} Y→{Y} Z→{Z}",
            ConfigType.Name, _config.SlaveAddress,
            _config.GetSubIndex(PressureChannel.X),
            _config.GetSubIndex(PressureChannel.Y),
            _config.GetSubIndex(PressureChannel.Z));

        await StartMonitoring();
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
    // 后台监控
    // ====================================================================

    public async Task StartMonitoring(int intervalMs = 100)
    {
        if (IsMonitoring) return;
        _cts = new CancellationTokenSource();
        IsMonitoring = true;
        Logger.Information("[{Type}] 后台轮询已启动，间隔 {Interval}ms", ConfigType.Name, intervalMs);
        _ = Task.Run(() => PollLoopAsync(intervalMs, _cts.Token), _cts.Token);
        await Task.CompletedTask;
    }

    public void StopMonitoring()
    {
        if (!IsMonitoring) return;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        IsMonitoring = false;
        Logger.Information("[{Type}] 后台轮询已停止", ConfigType.Name);
    }

    private async Task PollLoopAsync(int intervalMs, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(intervalMs, ct);

                var result = await ReadAllInternalAsync();
                if (!result.IsSuccess) continue;

                int oldX, oldY, oldZ;
                bool dataChanged;
                lock (_lock)
                {
                    oldX = _x; oldY = _y; oldZ = _z;
                    _x = result.Data.X;
                    _y = result.Data.Y;
                    _z = result.Data.Z;
                    dataChanged = oldX != _x || oldY != _y || oldZ != _z;
                }

                if (dataChanged)
                {
                    DataChanged?.Invoke(this, new PressureDataChangedEventArgs(_x, _y, _z));
                }

                // 报警检测（边沿触发）
                CheckAlarm(PressureChannel.X, _x, _config.GetAlarmThreshold(PressureChannel.X), ref _alarmX);
                CheckAlarm(PressureChannel.Y, _y, _config.GetAlarmThreshold(PressureChannel.Y), ref _alarmY);
                CheckAlarm(PressureChannel.Z, _z, _config.GetAlarmThreshold(PressureChannel.Z), ref _alarmZ);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Logger.Error(ex, "[{Type}] 轮询异常", ConfigType.Name);
            }
        }
    }

    private void CheckAlarm(PressureChannel channel, int value, int threshold, ref bool wasAlarmed)
    {
        if (threshold <= 0) return; // 阈值 0 = 禁用

        if (value > threshold && !wasAlarmed)
        {
            wasAlarmed = true;
            Logger.Warning("[{Type}] 通道 {Channel} 报警触发: 当前值={Value}, 阈值={Threshold}",
                ConfigType.Name, channel, value, threshold);
            AlarmTriggered?.Invoke(this, new PressureAlarmEventArgs(channel, value, threshold, true));
        }
        else if (value <= threshold && wasAlarmed)
        {
            wasAlarmed = false;
            Logger.Information("[{Type}] 通道 {Channel} 报警解除: 当前值={Value}, 阈值={Threshold}",
                ConfigType.Name, channel, value, threshold);
            AlarmTriggered?.Invoke(this, new PressureAlarmEventArgs(channel, value, threshold, false));
        }
    }

    // ====================================================================
    // 缓存值访问（非阻塞）
    // ====================================================================

    public int GetX() { lock (_lock) return _x; }
    public int GetY() { lock (_lock) return _y; }
    public int GetZ() { lock (_lock) return _z; }

    // ====================================================================
    // 按需读取
    // ====================================================================

    public Task<Result<int>> ReadXAsync() => ReadChannelAsync(PressureChannel.X);
    public Task<Result<int>> ReadYAsync() => ReadChannelAsync(PressureChannel.Y);
    public Task<Result<int>> ReadZAsync() => ReadChannelAsync(PressureChannel.Z);

    public async Task<Result<(int X, int Y, int Z)>> ReadAllAsync()
    {
        var result = await ReadAllInternalAsync();
        return result.IsSuccess
            ? Result<(int, int, int)>.Success((result.Data.X, result.Data.Y, result.Data.Z))
            : Result<(int, int, int)>.Fail(result.Message);
    }

    private async Task<Result<(int X, int Y, int Z)>> ReadAllInternalAsync()
    {
        var xTask = ReadChannelAsync(PressureChannel.X);
        var yTask = ReadChannelAsync(PressureChannel.Y);
        var zTask = ReadChannelAsync(PressureChannel.Z);

        await Task.WhenAll(xTask, yTask, zTask);

        if (!xTask.Result.IsSuccess)
            return Result<(int, int, int)>.Fail(xTask.Result.Message);
        if (!yTask.Result.IsSuccess)
            return Result<(int, int, int)>.Fail(yTask.Result.Message);
        if (!zTask.Result.IsSuccess)
            return Result<(int, int, int)>.Fail(zTask.Result.Message);

        return Result<(int, int, int)>.Success((xTask.Result.Data, yTask.Result.Data, zTask.Result.Data));
    }

    private async Task<Result<int>> ReadChannelAsync(PressureChannel channel)
    {
        if (!_motionCard.IsConnected)
            return Result<int>.Fail("运动控制卡未连接");

        if (_config.SlaveAddress == 0)
            return Result<int>.Fail("从站地址未配置");

        var subIndex = _config.GetSubIndex(channel);
        var result = await _motionCard.ReadTxPDOAsync(_config.SlaveAddress, OdReadPressure, subIndex, OdBitLen32);

        if (!result.IsSuccess)
            return Result<int>.Fail($"PDO 读取失败: {result.Message}");

        return Result<int>.Success(result.Data);
    }

    // ====================================================================
    // 清零校准
    // ====================================================================

    public Task<Result> ZeroXAsync() => ZeroChannelAsync(PressureChannel.X);
    public Task<Result> ZeroYAsync() => ZeroChannelAsync(PressureChannel.Y);
    public Task<Result> ZeroZAsync() => ZeroChannelAsync(PressureChannel.Z);

    public async Task<Result> ZeroAllAsync()
    {
        var results = await Task.WhenAll(ZeroXAsync(), ZeroYAsync(), ZeroZAsync());
        return results.All(r => r.IsSuccess)
            ? Result.Success("全部通道清零完成")
            : Result.Fail("部分通道清零失败");
    }

    private async Task<Result> ZeroChannelAsync(PressureChannel channel)
    {
        if (!_motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        if (_config.SlaveAddress == 0)
            return Result.Fail("从站地址未配置");

        var zeroValue = channel switch
        {
            PressureChannel.X => 0x5AA501,
            PressureChannel.Y => 0x5AA502,
            PressureChannel.Z => 0x5AA503,
            _ => throw new ArgumentOutOfRangeException(nameof(channel)),
        };

        Logger.Information("[{Type}] 通道 {Channel} 开始清零...", ConfigType.Name, channel);

        var result = await _motionCard.WriteRxPDOAsync(
            _config.SlaveAddress, OdZeroControl, OdSubIndex, OdBitLen32, zeroValue);

        if (!result.IsSuccess)
            return Result.Fail($"清零失败: {result.Message}");

        Logger.Information("[{Type}] 通道 {Channel} 清零完成", ConfigType.Name, channel);
        return Result.Success();
    }

    // ====================================================================
    // 配置读写
    // ====================================================================

    public PressureSensorConfig GetConfig() => _config;

    public async Task SaveConfigAsync(PressureSensorConfig config)
    {
        _config = config;
        await _configService.SaveAsync(ConfigType, _config);
        Logger.Information("[{Type}] 配置已保存", ConfigType.Name);
    }

    // ====================================================================
    // IDisposable
    // ====================================================================

    public void Dispose()
    {
        StopMonitoring();
        GC.SuppressFinalize(this);
    }
}
