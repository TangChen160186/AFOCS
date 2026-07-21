using System.ComponentModel.Composition;
using AFOCS.Devices.Implementation;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices
{
    // ============================================================
    // 轴状态变化事件
    // ============================================================

    /// <summary>轴类型</summary>
    public enum AxisKind { BusAxis, LinearAxis, Gripper }

    /// <summary>轴状态变化事件参数</summary>
    public class AxisStateChangedEventArgs : EventArgs
    {
        public AxisKind Kind { get; }
        public int AxisId { get; }
        public string Name { get; }
        public double Position { get; }
        public double Speed { get; }
        public bool IsEnabled { get; }
        public bool IsMoving { get; }
        public DateTime Timestamp { get; }

        public AxisStateChangedEventArgs(AxisKind kind, int axisId, string name,
            double position, double speed, bool isEnabled, bool isMoving)
        {
            Kind = kind;
            AxisId = axisId;
            Name = name;
            Position = position;
            Speed = speed;
            IsEnabled = isEnabled;
            IsMoving = isMoving;
            Timestamp = DateTime.Now;
        }
    }

    // ============================================================
    // 轴状态快照（用于批量查询）
    // ============================================================

    public class AxisSnapshot
    {
        public AxisKind Kind { get; init; }
        public int AxisId { get; init; }
        public string Name { get; init; } = "";
        public double Position { get; set; }
        public double Speed { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsMoving { get; set; }
    }

    // ============================================================
    // 轴状态服务接口
    // ============================================================

    public interface IAxisStateService
    {
        /// <summary>轴状态变化事件（位置/速度/使能/运动状态任一变化）</summary>
        event EventHandler<AxisStateChangedEventArgs>? StateChanged;

        /// <summary>启动位置轮询</summary>
        Task StartMonitor(int pollIntervalMs = 200);

        /// <summary>停止轮询</summary>
        void StopMonitor();

        bool IsMonitoring { get; }

        /// <summary>获取所有轴的当前快照（线程安全）</summary>
        IReadOnlyDictionary<(AxisKind Kind, int Id), AxisSnapshot> GetAllSnapshots();

        /// <summary>获取单个轴快照</summary>
        AxisSnapshot? GetSnapshot(AxisKind kind, int axisId);

        // -- 运动控制（总线轴） --
        Task MovePmoveAsync(AxisId axis, double distance, double? maxVelOverride = null);
        Task StopAxisAsync(AxisId axis);
        Task MoveHomeAsync(AxisId axis);

        // -- 直线轴（API 直连） —— 暂未实现 --
        Task MoveLinearPmoveAsync(LinearAxisId axis, double distance);

        // -- 夹爪 —— 暂未实现 --
        Task GripperGraspAsync(GripperId gripper);
        Task GripperReleaseAsync(GripperId gripper);
    }

    // ============================================================
    // 轴状态服务实现
    // ============================================================

    [Export(typeof(IAxisStateService))]
    [method: ImportingConstructor]
    public class AxisStateService(
        IMotionControlCard motionCard,
        IAxisConfigService axisConfigService,
        ISmcGripper smcGripper,
        ILogger logger) : IAxisStateService, IDisposable
    {
        private CancellationTokenSource? _cts;
        private readonly object _lock = new();
        private readonly Dictionary<(AxisKind, int), AxisSnapshot> _snapshots = [];
        private bool _disposed;

        public event EventHandler<AxisStateChangedEventArgs>? StateChanged;
        public bool IsMonitoring { get; private set; }

        // ---- 轮询 ----

        public async Task StartMonitor(int pollIntervalMs = 200)
        {
            if (IsMonitoring) return;

            InitSnapshots();
            _cts = new CancellationTokenSource();
            IsMonitoring = true;
            logger.Information("轴状态监控已启动，轮询间隔 {Interval}ms，共 {Count} 轴",
                pollIntervalMs, _snapshots.Count);

            _ = Task.Run(() => PollLoopAsync(pollIntervalMs, _cts.Token), _cts.Token);
            await Task.CompletedTask;
        }

        public void StopMonitor()
        {
            if (!IsMonitoring) return;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            IsMonitoring = false;
            logger.Information("轴状态监控已停止");
        }

        private void InitSnapshots()
        {
            lock (_lock)
            {
                _snapshots.Clear();

                foreach (AxisId id in Enum.GetValues<AxisId>())
                {
                    _snapshots[(AxisKind.BusAxis, (int)id)] = new AxisSnapshot
                    {
                        Kind = AxisKind.BusAxis,
                        AxisId = (int)id,
                        Name = GetBusAxisName(id),
                    };
                }

                foreach (LinearAxisId id in Enum.GetValues<LinearAxisId>())
                {
                    _snapshots[(AxisKind.LinearAxis, (int)id)] = new AxisSnapshot
                    {
                        Kind = AxisKind.LinearAxis,
                        AxisId = (int)id,
                        Name = GetLinearAxisName(id),
                    };
                }

                foreach (GripperId id in Enum.GetValues<GripperId>())
                {
                    _snapshots[(AxisKind.Gripper, (int)id)] = new AxisSnapshot
                    {
                        Kind = AxisKind.Gripper,
                        AxisId = (int)id,
                        Name = GetGripperName(id),
                    };
                }
            }
        }

        private async Task PollLoopAsync(int intervalMs, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(intervalMs, ct);

                    if (!motionCard.IsConnected) continue;

                    var changes = new List<AxisStateChangedEventArgs>();

                    // 只轮询总线轴（目前硬件可用）
                    foreach (AxisId id in Enum.GetValues<AxisId>())
                    {
                        ct.ThrowIfCancellationRequested();

                        var key = (AxisKind.BusAxis, (int)id);
                        AxisSnapshot snap;
                        lock (_lock)
                        {
                            if (!_snapshots.TryGetValue(key, out var s)) continue;
                            snap = s;
                        }

                        var posResult = await motionCard.GetPositionAsync((ushort)id);
                        if (!posResult.IsSuccess) continue;

                        var speedResult = await motionCard.GetSpeedAsync((ushort)id);
                        var speed = speedResult.IsSuccess ? speedResult.Data : 0;

                        double oldPos;
                        double oldSpeed;
                        bool changed;
                        lock (_lock)
                        {
                            oldPos = snap.Position;
                            oldSpeed = snap.Speed;
                            changed = Math.Abs(oldPos - posResult.Data) > 0.0001 ||
                                      Math.Abs(oldSpeed - speed) > 0.0001;

                            if (changed)
                            {
                                snap.Position = posResult.Data;
                                snap.Speed = speed;
                            }
                        }

                        if (changed)
                        {
                            // 尝试获取 enable 状态（失败就默认 false）
                            // 运动状态：speed 或 last position 变化判断
                            var isMoving = Math.Abs(speed) > 0.01;
                            changes.Add(new AxisStateChangedEventArgs(
                                snap.Kind, snap.AxisId, snap.Name,
                                snap.Position, snap.Speed, snap.IsEnabled, isMoving));
                        }
                    }

                    // 触发事件
                    foreach (var change in changes)
                    {
                        try { StateChanged?.Invoke(this, change); }
                        catch (Exception ex) { logger.Error(ex, "轴状态事件处理异常: {Axis}", change.Name); }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { logger.Error(ex, "轴轮询异常"); }
            }
        }

        // ---- 查询 ----

        public IReadOnlyDictionary<(AxisKind Kind, int Id), AxisSnapshot> GetAllSnapshots()
        {
            lock (_lock) return new Dictionary<(AxisKind, int), AxisSnapshot>(_snapshots);
        }

        public AxisSnapshot? GetSnapshot(AxisKind kind, int axisId)
        {
            lock (_lock)
                return _snapshots.TryGetValue((kind, axisId), out var snap) ? snap : null;
        }

        // ---- 运动控制（总线轴） ----

        public async Task MovePmoveAsync(AxisId axis, double distance, double? maxVelOverride = null)
        {
            var cfg = axisConfigService.GetConfig(axis).Motion;
            var result = await ((LeadShineMotionCard)motionCard).MovePmoveAsync(
                axis: (ushort)axis,
                distance: distance,
                equiv: cfg.Equiv,
                minVel: cfg.MinVel,
                maxVel: maxVelOverride ?? cfg.MaxVel,
                tacc: cfg.Tacc,
                tdec: cfg.Tdec,
                stopVel: cfg.StopVel,
                sPara: cfg.SPara);

            if (!result.IsSuccess)
                logger.Warning("轴移动失败 {Axis}: {Error}", axis, result.Message);
        }

        public async Task StopAxisAsync(AxisId axis)
        {
            var card = motionCard as Implementation.LeadShineMotionCard;
            if (card == null) return;
            await card.StopAxisAsync((ushort)axis);
        }

        public async Task MoveHomeAsync(AxisId axis)
        {
            var card = motionCard as Implementation.LeadShineMotionCard;
            if (card == null) return;
            var cfg = axisConfigService.GetConfig(axis).Home;
            await card.MoveHomeAsync(
                axis: (ushort)axis,
                homeMode: cfg.HomeMode,
                lowVel: cfg.LowVel,
                highVel: cfg.HighVel,
                tacc: cfg.Tacc,
                tdec: cfg.Tdec,
                offsetPos: cfg.OffsetPos);
        }

        // ---- 直线轴（暂未实现） ----

        public Task MoveLinearPmoveAsync(LinearAxisId axis, double distance)
        {
            logger.Warning("直线轴 {Axis} 移动暂未实现", axis);
            return Task.CompletedTask;
        }

        // ---- 夹爪 ----

        public async Task GripperGraspAsync(GripperId gripper)
        {
            var result = await smcGripper.Start(gripper);
            if (!result.IsSuccess)
                logger.Warning("夹爪 {Gripper} 定位失败: {Error}", gripper, result.Message);
        }

        public async Task GripperReleaseAsync(GripperId gripper)
        {
            var statusResult = await smcGripper.GetStatusAsync(gripper);
            if (statusResult.IsSuccess)
            {
                logger.Information("夹爪 {Gripper} 当前状态: 0x{Status:X4}", gripper, statusResult.Data);
            }
            else
            {
                logger.Warning("夹爪 {Gripper} 读取状态失败: {Error}", gripper, statusResult.Message);
            }
        }

        // ---- 名称映射 ----

        private static string GetBusAxisName(AxisId id) => id switch
        {
            AxisId.LeftCamUpX => "左上相机X轴",
            AxisId.LeftCamUpY => "左上相机Y轴",
            AxisId.LeftCamUpZ => "左上相机Z轴",
            AxisId.LeftCamSideY => "左侧相机Y轴",
            AxisId.LeftCouplingLThetaX => "左耦合左θX轴",
            AxisId.LeftCouplingLThetaY => "左耦合左θY轴",
            AxisId.LeftCouplingLThetaZ => "左耦合左θZ轴",
            AxisId.LeftCouplingRThetaX => "左耦合右θX轴",
            AxisId.LeftCouplingRThetaY => "左耦合右θY轴",
            AxisId.LeftCouplingRThetaZ => "左耦合右θZ轴",
            AxisId.RightCamUpX => "右上相机X轴",
            AxisId.RightCamUpY => "右上相机Y轴",
            AxisId.RightCamUpZ => "右上相机Z轴",
            AxisId.RightCamSideY => "右侧相机Y轴",
            AxisId.RightCouplingLThetaX => "右耦合左θX轴",
            AxisId.RightCouplingLThetaY => "右耦合左θY轴",
            AxisId.RightCouplingLThetaZ => "右耦合左θZ轴",
            AxisId.RightCouplingRThetaX => "右耦合右θX轴",
            AxisId.RightCouplingRThetaY => "右耦合右θY轴",
            AxisId.RightCouplingRThetaZ => "右耦合右θZ轴",
            _ => id.ToString(),
        };

        private static string GetLinearAxisName(LinearAxisId id) => id switch
        {
            LinearAxisId.LeftCouplingLX => "左工位左耦合X轴",
            LinearAxisId.LeftCouplingLY => "左工位左耦合Y轴",
            LinearAxisId.LeftCouplingLZ => "左工位左耦合Z轴",
            LinearAxisId.LeftCouplingRX => "左工位右耦合X轴",
            LinearAxisId.LeftCouplingRY => "左工位右耦合Y轴",
            LinearAxisId.LeftCouplingRZ => "左工位右耦合Z轴",
            LinearAxisId.RightCouplingLX => "右工位左耦合X轴",
            LinearAxisId.RightCouplingLY => "右工位左耦合Y轴",
            LinearAxisId.RightCouplingLZ => "右工位左耦合Z轴",
            LinearAxisId.RightCouplingRX => "右工位右耦合X轴",
            LinearAxisId.RightCouplingRY => "右工位右耦合Y轴",
            LinearAxisId.RightCouplingRZ => "右工位右耦合Z轴",
            _ => id.ToString(),
        };

        private static string GetGripperName(GripperId id) => id switch
        {
            GripperId.LeftCouplingLGripper => "左耦合左夹爪",
            GripperId.LeftCouplingRGripper => "左耦合右夹爪",
            GripperId.RightCouplingLGripper => "右耦合左夹爪",
            GripperId.RightCouplingRGripper => "右耦合右夹爪",
            _ => id.ToString(),
        };

        // ---- IDisposable ----

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopMonitor();
            GC.SuppressFinalize(this);
        }
    }
}
