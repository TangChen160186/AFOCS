using System.ComponentModel.Composition;
using AFOCS.Devices.MotionControlCard;
using AFOCS.Infrastructure;
using Serilog;
using AllInputs = AFOCS.Devices.IO.AllInputs;
using AllOutputs = AFOCS.Devices.IO.AllOutputs;

namespace AFOCS.Devices.IO;

[Export(typeof(IIoDevice))]
[method: ImportingConstructor]
public class IoDevice(IMotionControlCard motionCard, IConfigService configService, ILogger logger) : IIoDevice
{
    // ---- 连接状态 ----

    public bool IsConnected => motionCard.IsConnected;

    // ---- 配置 / 映射 ----

    private IoMappingConfig _config = IoMappingConfig.CreateDefault();

    private readonly Dictionary<AllInputs, int> _inputLookup = [];
    private readonly Dictionary<AllOutputs, int> _outputLookup = [];
    private readonly Dictionary<AllInputs, bool> _inputActiveLookup = [];
    private readonly Dictionary<AllOutputs, bool> _outputActiveLookup = [];

    public int GetInputBitNo(AllInputs signal) =>
        _inputLookup.TryGetValue(signal, out var bitNo) ? bitNo : (int)signal;

    public int GetOutputBitNo(AllOutputs signal) =>
        _outputLookup.TryGetValue(signal, out var bitNo) ? bitNo : (int)signal;

    public void SetInputBitNo(AllInputs signal, int bitNo)
    {
        _config.Inputs[signal.ToString()] = bitNo;
        _inputLookup[signal] = bitNo;
    }

    public void SetOutputBitNo(AllOutputs signal, int bitNo)
    {
        _config.Outputs[signal.ToString()] = bitNo;
        _outputLookup[signal] = bitNo;
    }

    public bool IsInputActiveHigh(AllInputs signal) =>
        _inputActiveLookup.TryGetValue(signal, out var activeHigh) ? activeHigh : true;

    public void SetInputActiveHigh(AllInputs signal, bool activeHigh)
    {
        _config.InputActives[signal.ToString()] = activeHigh;
        _inputActiveLookup[signal] = activeHigh;
    }

    public bool IsOutputActiveHigh(AllOutputs signal) =>
        _outputActiveLookup.TryGetValue(signal, out var activeHigh) ? activeHigh : true;

    public void SetOutputActiveHigh(AllOutputs signal, bool activeHigh)
    {
        _config.OutputActives[signal.ToString()] = activeHigh;
        _outputActiveLookup[signal] = activeHigh;
    }

    public IoMappingConfig GetConfig() => _config;

    public async Task LoadAsync()
    {
        try
        {
            var loaded = await configService.LoadAsync<IoMappingConfig>();
            if (loaded?.Inputs is { Count: > 0 } || loaded?.Outputs is { Count: > 0 })
            {
                _config = loaded;
                logger.Information("IO 配置已加载，输入 {InputCount} 项，输出 {OutputCount} 项",
                    _config.Inputs.Count, _config.Outputs.Count);
            }
            else
            {
                _config = IoMappingConfig.CreateDefault();
                await configService.SaveAsync(_config);
                logger.Information("IO 配置已初始化为默认值");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "加载 IO 配置失败，使用默认值");
            _config = IoMappingConfig.CreateDefault();
        }

        RebuildLookup();
    }

    public async Task SaveAsync()
    {
        foreach (var (signal, bitNo) in _inputLookup)
            _config.Inputs[signal.ToString()] = bitNo;
        foreach (var (signal, bitNo) in _outputLookup)
            _config.Outputs[signal.ToString()] = bitNo;
        foreach (var (signal, activeHigh) in _inputActiveLookup)
            _config.InputActives[signal.ToString()] = activeHigh;
        foreach (var (signal, activeHigh) in _outputActiveLookup)
            _config.OutputActives[signal.ToString()] = activeHigh;

        await configService.SaveAsync(_config);
        logger.Information("IO 配置已保存");
    }

    // ---- 输出读写 ----

    public async Task<Result> WriteOutputAsync(AllOutputs signal, bool on)
    {
        if (!motionCard.IsConnected)
        {
            logger.Warning("IO 输出失败: 雷赛控制卡未连接");
            return Result.Fail("IO 输出失败: 雷赛控制卡未连接");
        }
        var bitNo = GetOutputBitNo(signal);
        var rawValue = IsOutputActiveHigh(signal) ? on : !on;
        var result = await motionCard.WriteOutbitAsync((ushort)bitNo, rawValue);
        if (result.IsSuccess)
        {
            logger.Information("IO 输出: {Signal}(bit{No}) = {Value}(逻辑) raw={Raw}", signal, bitNo, on, rawValue);
            return Result.Success();
        }

        var msg = $"IO 输出失败: {signal} bit{bitNo}, {result.Message}";
        logger.Warning(msg);
        return Result.Fail(msg);
    }

    public async Task<Result<bool>> ReadOutputAsync(AllOutputs signal)
    {
        if (!motionCard.IsConnected)
            return Result<bool>.Fail("IO 设备未连接: 雷赛控制卡未连接");

        var bitNo = GetOutputBitNo(signal);
        var result = await motionCard.ReadOutbitAsync((ushort)bitNo);
        if (!result.IsSuccess)
            return Result<bool>.Fail($"读取输出 {signal} 失败: {result.Message}");

        return Result<bool>.Success(IsOutputActiveHigh(signal) ? result.Data : !result.Data);
    }

    public async Task<Result<bool>> ReadOutputRawAsync(AllOutputs signal)
    {
        if (!motionCard.IsConnected)
            return Result<bool>.Fail("IO 设备未连接: 雷赛控制卡未连接");

        var bitNo = GetOutputBitNo(signal);
        var result = await motionCard.ReadOutbitAsync((ushort)bitNo);
        if (!result.IsSuccess)
            return Result<bool>.Fail($"读取输出 {signal} 失败: {result.Message}");

        return Result<bool>.Success(result.Data);
    }

    // ---- 输入监控 ----

    private CancellationTokenSource? _cts;
    private readonly bool[] _states = new bool[136];
    private readonly object _lock = new();
    private readonly List<AllInputs>[] _bitToSignals = new List<AllInputs>[136];

    public event EventHandler<IoStateChangedEventArgs>? InputChanged;
    public bool IsMonitoring { get; private set; }

    public bool GetState(AllInputs signal)
    {
        var bitNo = GetInputBitNo(signal);
        lock (_lock)
            return ToLogical(signal, _states[bitNo]);
    }

    public bool GetRawState(AllInputs signal)
    {
        var bitNo = GetInputBitNo(signal);
        lock (_lock)
            return _states[bitNo];
    }

    public async Task StartMonitor(int pollIntervalMs = 100)
    {
        if (IsMonitoring) return;

        await LoadAsync();
        BuildInputMapping();
        _cts = new CancellationTokenSource();
        IsMonitoring = true;
        logger.Information("IO 监控已启动，轮询间隔 {Interval}ms", pollIntervalMs);

        _ = Task.Run(() => PollLoopAsync(pollIntervalMs, _cts.Token), _cts.Token);
    }

    public void StopMonitor()
    {
        if (!IsMonitoring) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        IsMonitoring = false;
        logger.Information("IO 监控已停止");
    }

    private void BuildInputMapping()
    {
        Array.Clear(_bitToSignals);
        for (int i = 0; i < _bitToSignals.Length; i++)
            _bitToSignals[i] = [];

        foreach (AllInputs signal in Enum.GetValues<AllInputs>())
        {
            var bitNo = GetInputBitNo(signal);
            if (bitNo >= 0 && bitNo < 136)
                _bitToSignals[bitNo].Add(signal);
        }
    }

    private async Task PollLoopAsync(int intervalMs, CancellationToken ct)
    {
        try
        {
            var initResult = await motionCard.ReadInbitsAsync(136);
            if (initResult.IsSuccess)
            {
                lock (_lock)
                    Array.Copy(initResult.Data, _states, Math.Min(initResult.Data.Length, _states.Length));
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "IO 初始状态读取失败");
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(intervalMs, ct);

                var result = await motionCard.ReadInbitsAsync(136);
                if (!result.IsSuccess)
                {
                    logger.Warning("IO 轮询读取失败: {Error}", result.Message);
                    continue;
                }

                DetectChanges(result.Data);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.Error(ex, "IO 轮询异常"); }
        }
    }

    private bool ToLogical(AllInputs signal, bool raw) =>
        IsInputActiveHigh(signal) ? raw : !raw;

    private void DetectChanges(bool[] newRawStates)
    {
        var changes = new List<IoStateChangedEventArgs>();

        lock (_lock)
        {
            var len = Math.Min(newRawStates.Length, _states.Length);
            for (int i = 0; i < len; i++)
            {
                if (_states[i] == newRawStates[i]) continue;

                var signals = _bitToSignals[i];
                foreach (var signal in signals)
                {
                    changes.Add(new IoStateChangedEventArgs(
                        signal,
                        ToLogical(signal, _states[i]),
                        ToLogical(signal, newRawStates[i])));
                }
                _states[i] = newRawStates[i];
            }
        }

        foreach (var change in changes)
        {
            try { InputChanged?.Invoke(this, change); }
            catch (Exception ex) { logger.Error(ex, "IO 事件处理异常: {Signal}", change.Signal); }
        }
    }

    // ---- 内部 ----

    private void RebuildLookup()
    {
        _inputLookup.Clear();
        _outputLookup.Clear();
        _inputActiveLookup.Clear();
        _outputActiveLookup.Clear();

        foreach (var kv in _config.Inputs)
        {
            if (Enum.TryParse<AllInputs>(kv.Key, out var signal))
                _inputLookup[signal] = kv.Value;
        }
        foreach (var kv in _config.Outputs)
        {
            if (Enum.TryParse<AllOutputs>(kv.Key, out var signal))
                _outputLookup[signal] = kv.Value;
        }

        if (_config.InputActives.Count == 0)
        {
            foreach (var signal in Enum.GetValues<AllInputs>())
                _config.InputActives[signal.ToString()] = true;
        }
        if (_config.OutputActives.Count == 0)
        {
            foreach (var signal in Enum.GetValues<AllOutputs>())
                _config.OutputActives[signal.ToString()] = true;
        }

        foreach (var kv in _config.InputActives)
        {
            if (Enum.TryParse<AllInputs>(kv.Key, out var signal))
                _inputActiveLookup[signal] = kv.Value;
        }
        foreach (var kv in _config.OutputActives)
        {
            if (Enum.TryParse<AllOutputs>(kv.Key, out var signal))
                _outputActiveLookup[signal] = kv.Value;
        }
    }

    // ---- IDevice ----

    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        if (!motionCard.IsConnected)
            return Result.Fail("IO 设备初始化失败: 雷赛控制卡未连接或总线异常");

        try
        {
            await StartMonitor();
            logger.Information("IO 设备初始化成功");
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "IO 设备初始化异常");
            return Result.Fail($"IO 设备初始化异常: {ex.Message}");
        }
    }

    public async Task<Result> StopAsync(CancellationToken token = default)
    {
        StopMonitor();
        logger.Information("IO 设备已停止");
        return await Task.FromResult(Result.Success());
    }

    public async Task<Result> ReConnectAsync(CancellationToken token = default)
    {
        await StopAsync(token);
        return await InitializeAsync(token);
    }

    public void Dispose()
    {
        StopMonitor();
        _cts?.Dispose();
    }
}