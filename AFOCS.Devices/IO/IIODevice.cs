using AFOCS.Infrastructure;
using AllInputs = AFOCS.Devices.IO.AllInputs;
using AllOutputs = AFOCS.Devices.IO.AllOutputs;

namespace AFOCS.Devices.IO;

public class IoStateChangedEventArgs(AllInputs signal, bool oldValue, bool newValue) : EventArgs
{
    public AllInputs Signal { get; } = signal;
    public bool OldValue { get; } = oldValue;
    public bool NewValue { get; } = newValue;
    public DateTime Timestamp { get; } = DateTime.Now;

    public bool IsRisingEdge => !OldValue && NewValue;
    public bool IsFallingEdge => OldValue && !NewValue;
}

/// <summary>
/// IO 设备接口 —— 集成所有 IO 映射、监控、配置功能
/// </summary>
public interface IIoDevice : IDevice
{
    // -- 输入监控 --
    event EventHandler<IoStateChangedEventArgs>? InputChanged;
    Task StartMonitor(int pollIntervalMs = 100);
    void StopMonitor();
    bool IsMonitoring { get; }
    bool GetState(AllInputs signal);
    bool GetRawState(AllInputs signal);

    // -- 输出读写 --
    Task<Result> WriteOutputAsync(AllOutputs signal, bool on);
    Task<Result<bool>> ReadOutputAsync(AllOutputs signal);
    Task<Result<bool>> ReadOutputRawAsync(AllOutputs signal);

    // -- 位号映射 --
    int GetInputBitNo(AllInputs signal);
    int GetOutputBitNo(AllOutputs signal);
    void SetInputBitNo(AllInputs signal, int bitNo);
    void SetOutputBitNo(AllOutputs signal, int bitNo);

    // -- 有效电平 --
    bool IsInputActiveHigh(AllInputs signal);
    void SetInputActiveHigh(AllInputs signal, bool activeHigh);
    bool IsOutputActiveHigh(AllOutputs signal);
    void SetOutputActiveHigh(AllOutputs signal, bool activeHigh);

    // -- 配置持久化 --
    IoMappingConfig GetConfig();
    Task LoadAsync();
    Task SaveAsync();
}