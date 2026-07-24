using AFOCS.Devices.Implementation;
using AFOCS.Infrastructure;

namespace AFOCS.Devices
{
    public class MotionCardConnectionChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public MotionCardConnectionChangedEventArgs(bool isConnected) => IsConnected = isConnected;
    }

    public interface IMotionControlCard : IDevice
    {
        event EventHandler<MotionCardConnectionChangedEventArgs>? ConnectionChanged;

        Task<Result> HotResetAsync();
        Task<Result> ColdResetAsync();

        /// <summary>获取总线错误码和状态描述</summary>
        Task<Result<(ushort ErrorCode, string Description)>> GetBusStatusAsync();

        LeadShineMotionCardConfig GetConfig();
        Task SaveConfigAsync(LeadShineMotionCardConfig config);

        // ========== 轴状态监控 ==========

        /// <summary>轴状态变化事件</summary>
        event EventHandler<AxisStateChangedEventArgs>? AxisStateChanged;

        /// <summary>是否正在轮询轴状态</summary>
        bool IsAxisMonitoring { get; }

        /// <summary>启动轴位置/速度轮询</summary>
        Task StartAxisMonitorAsync(int pollIntervalMs = 200);

        /// <summary>停止轴轮询</summary>
        void StopAxisMonitor();

        // ========== 轴配置管理 ==========

        /// <summary>获取指定轴的配置</summary>
        AxisConfig GetAxisConfig(AxisId axisId);

        /// <summary>更新指定轴的配置（需调用 SaveAllAxisConfigsAsync 持久化）</summary>
        void SetAxisConfig(AxisId axisId, AxisConfig config);

        /// <summary>获取指定轴的默认配置</summary>
        AxisConfig GetDefaultAxisConfig(AxisId axisId);

        /// <summary>加载所有轴配置</summary>
        Task LoadAllAxisConfigsAsync();

        /// <summary>保存所有轴配置</summary>
        Task SaveAllAxisConfigsAsync();

        /// <summary>读取单个输入位</summary>
        Task<Result<bool>> ReadInbitAsync(ushort bitNo);

        /// <summary>批量读取多个输入位（0~bitCount-1），返回位数组</summary>
        Task<Result<bool[]>> ReadInbitsAsync(ushort bitCount);

        /// <summary>读取单个输出位当前电平</summary>
        Task<Result<bool>> ReadOutbitAsync(ushort bitNo);

        /// <summary>设置单个输出位</summary>
        Task<Result> WriteOutbitAsync(ushort bitNo, bool on);

        /// <summary>读取轴当前位置（unit）</summary>
        Task<Result<double>> GetPositionAsync(ushort axis);

        /// <summary>读取轴当前速度（unit/s）</summary>
        Task<Result<double>> GetSpeedAsync(ushort axis);

        /// <summary>定长运动（点位运动）</summary>
        Task<Result> MovePmoveAsync(ushort axis, double distance, ushort posiMode = 0,
            double equiv = 1000.0, double minVel = 10, double maxVel = 3000,
            double tacc = 0.1, double tdec = 0.1, double stopVel = 10,
            double sPara = 0, int timeoutMs = 0);

        /// <summary>回零运动</summary>
        Task<Result> MoveHomeAsync(ushort axis, ushort homeMode = 33,
            double lowVel = 100, double highVel = 1000, double tacc = 0.1,
            double tdec = 0.1, double offsetPos = 0, double equiv = 1000.0, int timeoutMs = 30000);

        /// <summary>停止轴运动</summary>
        Task<Result> StopAxisAsync(ushort axis, bool emergency = false);


        // --- PDO 读写（按 OD 地址直接操作） ---

        /// <summary>写从站 RxPDO（按 index/subindex 指定 OD 地址）</summary>
        Task<Result> WriteRxPDOAsync(ushort slaveAddr, ushort index, ushort subIndex, ushort bitLength, int value);

        /// <summary>读从站 TxPDO（按 index/subindex 指定 OD 地址）</summary>
        Task<Result<int>> ReadTxPDOAsync(ushort slaveAddr, ushort index, ushort subIndex, ushort bitLength);
    }
}
