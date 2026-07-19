using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices
{
    /// <summary>
    /// IO 状态变化事件参数
    /// </summary>
    public class IOStateChangedEventArgs : EventArgs
    {
        public AllInputs Signal { get; }
        public bool OldValue { get; }
        public bool NewValue { get; }
        public DateTime Timestamp { get; }

        /// <summary>是否为上升沿（0→1）</summary>
        public bool IsRisingEdge => !OldValue && NewValue;
        /// <summary>是否为下降沿（1→0）</summary>
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
    /// IO 监控服务 —— 持续轮询所有输入，状态变化时触发事件
    /// 通过 IIOMappingService 解析信号→位号映射
    /// </summary>
    public interface IIOMonitorService
    {
        /// <summary>IO 状态变化事件（任意输入位变化时触发）</summary>
        event EventHandler<IOStateChangedEventArgs>? InputChanged;

        /// <summary>启动监控（后台轮询）</summary>
        void Start(int pollIntervalMs = 100);

        /// <summary>停止监控</summary>
        void Stop();

        /// <summary>是否正在运行</summary>
        bool IsRunning { get; }

        /// <summary>获取指定信号的当前状态</summary>
        bool GetState(AllInputs signal);
    }

    [Export(typeof(IIOMonitorService))]
    [method: ImportingConstructor]
    public class IOMonitorService(IMotionControlCard motionCard, IIOMappingService mappingService, ILogger logger)
        : IIOMonitorService, IDisposable
    {
        private CancellationTokenSource? _cts;
        private readonly bool[] _states = new bool[128];
        private readonly object _lock = new();

        // 位号 → 信号列表（一位可能对应多个信号，通常一对一）
        private readonly List<AllInputs>[] _bitToSignals = new List<AllInputs>[128];

        public event EventHandler<IOStateChangedEventArgs>? InputChanged;
        public bool IsRunning { get; private set; }

        public bool GetState(AllInputs signal)
        {
            var bitNo = mappingService.GetInputBitNo(signal);
            lock (_lock)
                return _states[bitNo];
        }

        public void Start(int pollIntervalMs = 100)
        {
            if (IsRunning) return;

            BuildMapping();
            _cts = new CancellationTokenSource();
            IsRunning = true;
            logger.Information("IO 监控服务已启动，轮询间隔 {Interval}ms", pollIntervalMs);

            _ = Task.Run(() => PollLoopAsync(pollIntervalMs, _cts.Token), _cts.Token);
        }

        public void Stop()
        {
            if (!IsRunning) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            IsRunning = false;
            logger.Information("IO 监控服务已停止");
        }

        private void BuildMapping()
        {
            Array.Clear(_bitToSignals);
            for (int i = 0; i < _bitToSignals.Length; i++)
                _bitToSignals[i] = [];

            foreach (AllInputs signal in Enum.GetValues<AllInputs>())
            {
                var bitNo = mappingService.GetInputBitNo(signal);
                if (bitNo >= 0 && bitNo < 128)
                    _bitToSignals[bitNo].Add(signal);
            }
        }

        private async Task PollLoopAsync(int intervalMs, CancellationToken ct)
        {
            // 首次读取初始状态（不触发事件）
            try
            {
                var initResult = await motionCard.ReadInbitsAsync(128);
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

                    var result = await motionCard.ReadInbitsAsync(128);
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

        private void DetectChanges(bool[] newStates)
        {
            var changes = new List<IOStateChangedEventArgs>();

            lock (_lock)
            {
                var len = Math.Min(newStates.Length, _states.Length);
                for (int i = 0; i < len; i++)
                {
                    if (_states[i] == newStates[i]) continue;

                    // 查找映射到此位的所有信号
                    var signals = _bitToSignals[i];
                    foreach (var signal in signals)
                    {
                        changes.Add(new IOStateChangedEventArgs(signal, _states[i], newStates[i]));
                    }
                    _states[i] = newStates[i];
                }
            }

            foreach (var change in changes)
            {
                try { InputChanged?.Invoke(this, change); }
                catch (Exception ex) { logger.Error(ex, "IO 事件处理异常: {Signal}", change.Signal); }
            }
        }

        public void Dispose() => Stop();
    }
}
