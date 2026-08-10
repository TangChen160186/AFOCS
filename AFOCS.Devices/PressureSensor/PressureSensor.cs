using AFOCS.Devices.MotionControlCard;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.PressureSensor;

public abstract class PressureSensor<TConfig>(IMotionControlCard motionCard, IConfigService configService, ILogger logger)
    : IPressureSensor
where TConfig : PressureSensorConfig
{
    private PressureSensorConfig _config = new();
    private CancellationTokenSource? _cts;
    private readonly Lock _lock = new();
    private int _x, _y, _z;

    private bool _alarmX, _alarmY, _alarmZ;

    // OD 地址常量
    private const ushort OdReadPressure = 0x6000;
    private const ushort OdZeroControl = 0x7000;
    private const ushort OdSubIndex = 0x01;
    private const ushort OdBitLen32 = 32;

    public bool IsConnected => motionCard.IsConnected;
    public abstract WorkPos WorkPos { get; }
    public bool IsMonitoring { get; private set; }
    public abstract PressureSensorType SensorType { get; }

    public event EventHandler<PressureDataChangedEventArgs>? DataChanged;
    public event EventHandler<PressureAlarmEventArgs>? AlarmTriggered;

    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        var loaded = await configService.LoadAsync(typeof(TConfig));
        if (loaded is PressureSensorConfig config)
            _config = config;
        else
        {
            _config = (PressureSensorConfig)Activator.CreateInstance(typeof(TConfig))!;
            await configService.SaveAsync(typeof(TConfig), _config);
        }

        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接，压力传感器无法初始化");
        logger.Information("[{Type}] 初始化完成，从站地址={Addr}, 通道映射 X→{X} Y→{Y} Z→{Z}",
            GetType().Name, _config.SlaveAddress,
            _config.GetSubIndex(PressureChannel.X),
            _config.GetSubIndex(PressureChannel.Y),
            _config.GetSubIndex(PressureChannel.Z));

        await StartMonitoring();
        return Result.Success();
    }


    public Task<Result> ReConnectAsync(CancellationToken token = default)
        => InitializeAsync(token);


    public async Task StartMonitoring(int intervalMs = 100)
    {
        if (IsMonitoring) return;
        _cts = new CancellationTokenSource();
        IsMonitoring = true;
        logger.Information("[{Type}] 后台轮询已启动，间隔 {Interval}ms", GetType().Name, intervalMs);
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
        logger.Information("[{Type}] 后台轮询已停止", GetType().Name);
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

             
                lock (_lock)
                {
                    _x = result.Data.X;
                    _y = result.Data.Y;
                    _z = result.Data.Z;
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
                logger.Error(ex, "[{Type}] 轮询异常", GetType().Name);
            }
        }
    }

    private void CheckAlarm(PressureChannel channel, int value, int threshold, ref bool wasAlarmed)
    {
        if (threshold <= 0) return; // 阈值 0 = 禁用

        if (value > threshold && !wasAlarmed)
        {
            wasAlarmed = true;
            logger.Warning("[{Type}] 通道 {Channel} 报警触发: 当前值={Value}, 阈值={Threshold}",
                GetType().Name, channel, value, threshold);
            AlarmTriggered?.Invoke(this, new PressureAlarmEventArgs(channel, value, threshold, true));
        }
        else if (value <= threshold && wasAlarmed)
        {
            wasAlarmed = false;
            logger.Information("[{Type}] 通道 {Channel} 报警解除: 当前值={Value}, 阈值={Threshold}",
                GetType().Name, channel, value, threshold);
            AlarmTriggered?.Invoke(this, new PressureAlarmEventArgs(channel, value, threshold, false));
        }
    }


    public int GetX() { lock (_lock) return _x; }
    public int GetY() { lock (_lock) return _y; }
    public int GetZ() { lock (_lock) return _z; }



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

        await Task.WhenAll(xTask, yTask, zTask); // 并发读取

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
        if (!motionCard.IsConnected)
            return Result<int>.Fail("运动控制卡未连接");

        if (_config.SlaveAddress == 0)
            return Result<int>.Fail("从站地址未配置");

        var subIndex = _config.GetSubIndex(channel);
        var result = await motionCard.ReadTxPDOAsync(_config.SlaveAddress, OdReadPressure, subIndex, OdBitLen32);

        if (!result.IsSuccess)
            return Result<int>.Fail($"PDO 读取失败: {result.Message}");

        return Result<int>.Success(result.Data);
    }

    public async Task<Result> ZeroAllAsync()
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        if (_config.SlaveAddress == 0)
            return Result.Fail("从站地址未配置");


        var result = await motionCard.WriteRxPDOAsync(
            _config.SlaveAddress, OdZeroControl, OdSubIndex, OdBitLen32, 0);
        if (!result.IsSuccess)
            return Result.Fail($"清零写入 0x00 失败: {result.Message}");

        result = await motionCard.WriteRxPDOAsync(
            _config.SlaveAddress, OdZeroControl, OdSubIndex, OdBitLen32, 0x5AA500);
        if (!result.IsSuccess)
            return Result.Fail($"清零写入 0x{0x5AA501:X} 失败: {result.Message}");

        logger.Information("所有通道清零完成");
        return Result.Success();
    }

    

    public PressureSensorConfig GetConfig() => _config.Clone();

    public async Task SaveConfigAsync(PressureSensorConfig config)
    {
        _config = config.Clone();
        await configService.SaveAsync(typeof(TConfig), _config);
    }


    public void Dispose()
    {
        StopMonitoring();
        GC.SuppressFinalize(this);
    }
}
