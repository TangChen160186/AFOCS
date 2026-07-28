using AFOCS.Devices.Implementation;
using AFOCS.Infrastructure;

namespace AFOCS.Devices
{
    public interface IBusAxisDevice : IDevice
    {
        // ========== 轴状态监控 ==========

        event EventHandler<BusAxisStateChangedEventArgs>? AxisStateChanged;
        bool IsAxisMonitoring { get; }
        Task StartAxisMonitorAsync(int pollIntervalMs = 200);
        void StopAxisMonitor();

        // ========== 轴配置管理 ==========

        AxisConfig GetAxisConfig(AxisId axisId);
        void SetAxisConfig(AxisId axisId, AxisConfig config);
        AxisConfig GetDefaultAxisConfig(AxisId axisId);
        Task LoadAllAxisConfigsAsync();
        Task SaveAllAxisConfigsAsync();

        // ========== 运动控制 ==========

        /// <summary>定长运动（点位运动），参数为 null 时自动从 AxisConfig 读取默认值</summary>
        Task<Result> MovePmoveAsync(AxisId axisId, double distance,
            ushort posiMode = 0,
            double? minVel = null,
            double? maxVel = null,
            double? tacc = null,
            double? tdec = null,
            double? stopVel = null,
            double? sPara = null,
            int timeoutMs = 0);

        /// <summary>回零运动，参数为 null 时自动从 AxisConfig 读取默认值</summary>
        Task<Result> MoveHomeAsync(AxisId axisId,
            ushort? homeMode = null,
            double? lowVel = null,
            double? highVel = null,
            double? tacc = null,
            double? tdec = null,
            double? offsetPos = null,
            int timeoutMs = 30000);

        /// <summary>停止指定轴</summary>
        Task<Result> StopAxisAsync(AxisId axisId, bool emergency = false);

        /// <summary>紧急停止所有轴</summary>
        Task<Result> EmergencyStopAllAsync();

        /// <summary>直线插补（多轴同步），参数为 null 时自动从第一个轴的 AxisConfig 读取默认值</summary>
        Task<Result> MoveLineAsync(AxisId[] axisList, double[] targetPositions,
            ushort posiMode = 0,
            double? minVel = null,
            double? maxVel = null,
            double? tacc = null,
            double? tdec = null,
            double? stopVel = null,
            double? sPara = null,
            int timeoutMs = 0);

        /// <summary>读取轴当前位置</summary>
        Task<Result<double>> GetPositionAsync(AxisId axisId);

        /// <summary>读取轴当前速度</summary>
        Task<Result<double>> GetSpeedAsync(AxisId axisId);

        /// <summary>设置软限位</summary>
        Task<Result> SetSoftLimitAsync(AxisId axisId);

        /// <summary>使能轴</summary>
        Task<Result> EnableAxisAsync(AxisId axisId, int timeoutMs = 3000);

        /// <summary>失能轴</summary>
        Result DisableAxis(AxisId axisId);
    }
}
