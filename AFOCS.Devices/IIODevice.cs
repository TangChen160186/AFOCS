using AFOCS.Infrastructure;
using AllInputs = AFOCS.Devices.Enums.AllInputs;
using AllOutputs = AFOCS.Devices.Enums.AllOutputs;

namespace AFOCS.Devices
{
    /// <summary>
    /// IO 状态变化事件参数（OldValue/NewValue 均为逻辑值，已考虑有效电平）
    /// </summary>
    public class IOStateChangedEventArgs : EventArgs
    {
        public AllInputs Signal { get; }
        public bool OldValue { get; }
        public bool NewValue { get; }
        public DateTime Timestamp { get; }

        public bool IsRisingEdge => !OldValue && NewValue;
        public bool IsFallingEdge => OldValue && !NewValue;

        public IOStateChangedEventArgs(AllInputs signal, bool oldValue, bool newValue)
        {
            Signal = signal;
            OldValue = oldValue;
            NewValue = newValue;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// IO 设备接口 —— 集成所有 IO 映射、监控、配置功能
    /// </summary>
    public interface IIODevice : IDevice
    {
        // -- 输入监控 --
        event EventHandler<IOStateChangedEventArgs>? InputChanged;
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
        IOMappingConfig GetConfig();
        Task LoadAsync();
        Task SaveAsync();
    }
}
