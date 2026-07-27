using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation
{
    [Export(typeof(IBusAxisDevice))]
    [method: ImportingConstructor]
    public class BusAxisDevice(IMotionControlCard motionCard, IConfigService configService, ILogger logger) : IBusAxisDevice
    {
        private readonly Dictionary<AxisId, AxisConfig> _axisConfigs = [];
        private CancellationTokenSource? _monitorCts;
        private bool _isAxisMonitoring;

        // ========== IDevice ==========

        public bool IsConnected => motionCard.IsConnected;

        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            if (!motionCard.IsConnected)
                return Result.Fail("总线轴设备初始化失败: 雷赛控制卡未连接");

            try
            {
                await LoadAllAxisConfigsAsync();
                logger.Information("总线轴设备初始化成功，共 {Count} 个轴", _axisConfigs.Count);
                return Result.Success();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "总线轴设备初始化异常");
                return Result.Fail($"总线轴设备初始化异常: {ex.Message}");
            }
        }

        public async Task<Result> StopAsync(CancellationToken token = default)
        {
            StopAxisMonitor();
            logger.Information("总线轴设备已停止");
            return await Task.FromResult(Result.Success());
        }

        public async Task<Result> ReConnectAsync(CancellationToken token = default)
        {
            await StopAsync(token);
            return await InitializeAsync(token);
        }

        public void Dispose()
        {
            StopAxisMonitor();
            _monitorCts?.Dispose();
        }

        // ========== 轴配置管理 ==========

        public AxisConfig GetAxisConfig(AxisId axisId)
        {
            if (_axisConfigs.TryGetValue(axisId, out var config))
                return config;

            var defaults = GetDefaultAxisConfig(axisId);
            _axisConfigs[axisId] = defaults;
            return defaults;
        }

        public void SetAxisConfig(AxisId axisId, AxisConfig config)
        {
            _axisConfigs[axisId] = config.Clone();
        }

        public async Task LoadAllAxisConfigsAsync()
        {
            try
            {
                var collection = await configService.LoadAsync<AxisConfigCollection>();
                if (collection?.Axes != null)
                {
                    foreach (var (key, config) in collection.Axes)
                        _axisConfigs[(AxisId)key] = config;
                }

                foreach (AxisId axisId in Enum.GetValues<AxisId>())
                {
                    if (!_axisConfigs.ContainsKey(axisId))
                        _axisConfigs[axisId] = GetDefaultAxisConfig(axisId);
                }

                logger.Information("轴配置加载完成，共 {Count} 个轴", _axisConfigs.Count);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "加载轴配置失败");
                foreach (AxisId axisId in Enum.GetValues<AxisId>())
                    _axisConfigs[axisId] = GetDefaultAxisConfig(axisId);
            }
        }

        public async Task SaveAllAxisConfigsAsync()
        {
            var collection = new AxisConfigCollection
            {
                Axes = _axisConfigs.ToDictionary(kv => (int)kv.Key, kv => kv.Value)
            };
            await configService.SaveAsync(collection);
            logger.Information("轴配置已保存");
        }

        public AxisConfig GetDefaultAxisConfig(AxisId axisId)
        {
            var config = new AxisConfig { AxisId = axisId };

            switch (axisId)
            {
                case AxisId.LeftCamUpX:
                case AxisId.RightCamUpX:
                    config.Motion.Equiv = 1000;
                    config.Motion.MaxVel = 10;
                    config.Home.HomeMode = 33;
                    config.PulsePerRev = 20000;
                    break;
                case AxisId.LeftCamUpY:
                case AxisId.RightCamUpY:
                case AxisId.LeftCamSideY:
                case AxisId.RightCamSideY:
                    config.Motion.Equiv = 1000;
                    config.Motion.MaxVel = 100;
                    config.Home.HomeMode = 33;
                    config.PulsePerRev = 10000;
                    break;
                case AxisId.LeftCamUpZ:
                case AxisId.RightCamUpZ:
                    config.Motion.Equiv = 1000;
                    config.Motion.MaxVel = 50;
                    config.Home.HomeMode = 33;
                    config.PulsePerRev = 5000;
                    break;
                case AxisId.LeftCouplingLThetaX:
                case AxisId.LeftCouplingLThetaY:
                case AxisId.LeftCouplingLThetaZ:
                case AxisId.LeftCouplingRThetaX:
                case AxisId.LeftCouplingRThetaY:
                case AxisId.LeftCouplingRThetaZ:
                case AxisId.RightCouplingLThetaX:
                case AxisId.RightCouplingLThetaY:
                case AxisId.RightCouplingLThetaZ:
                case AxisId.RightCouplingRThetaX:
                case AxisId.RightCouplingRThetaY:
                case AxisId.RightCouplingRThetaZ:
                    config.Motion.Equiv = 10000.0 / 360.0;
                    config.Motion.MaxVel = 30;
                    config.Motion.MinVel = 1;
                    config.Home.HomeMode = 33;
                    config.Home.LowVel = 1;
                    config.Home.HighVel = 10;
                    config.PulsePerRev = 10000;
                    break;
            }

            return config;
        }

        // ========== 轴状态监控 ==========

        public event EventHandler<AxisStateChangedEventArgs>? AxisStateChanged;
        public bool IsAxisMonitoring => _isAxisMonitoring;

        public Task StartAxisMonitorAsync(int pollIntervalMs = 200)
        {
            if (_isAxisMonitoring) return Task.CompletedTask;

            _monitorCts = new CancellationTokenSource();
            _isAxisMonitoring = true;
            logger.Information("轴状态监控已启动，轮询间隔 {Interval}ms", pollIntervalMs);

            _ = Task.Run(() => PollLoopAsync(pollIntervalMs, _monitorCts.Token), _monitorCts.Token);
            return Task.CompletedTask;
        }

        public void StopAxisMonitor()
        {
            if (!_isAxisMonitoring) return;
            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
            _monitorCts = null;
            _isAxisMonitoring = false;
            logger.Information("轴状态监控已停止");
        }

        private async Task PollLoopAsync(int intervalMs, CancellationToken ct)
        {
            var lastPositions = new Dictionary<AxisId, double>();
            var lastSpeeds = new Dictionary<AxisId, double>();

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(intervalMs, ct);

                    if (!motionCard.IsConnected) continue;

                    var changes = new List<AxisStateChangedEventArgs>();

                    foreach (AxisId id in Enum.GetValues<AxisId>())
                    {
                        ct.ThrowIfCancellationRequested();

                        var posResult = await motionCard.GetPositionAsync((ushort)id);
                        if (!posResult.IsSuccess) continue;

                        var speedResult = await motionCard.GetSpeedAsync((ushort)id);
                        var speed = speedResult.IsSuccess ? speedResult.Data : 0;

                        lastPositions.TryGetValue(id, out var oldPos);
                        lastSpeeds.TryGetValue(id, out var oldSpeed);

                        var changed = Math.Abs(oldPos - posResult.Data) > 0.0001 ||
                                      Math.Abs(oldSpeed - speed) > 0.0001;

                        if (changed)
                        {
                            lastPositions[id] = posResult.Data;
                            lastSpeeds[id] = speed;

                            var isMoving = Math.Abs(speed) > 0.01;
                            var name = GetAxisDisplayName(id);

                            changes.Add(new AxisStateChangedEventArgs(
                                AxisKind.BusAxis, (int)id, name,
                                posResult.Data, speed, true, isMoving));
                        }
                    }

                    foreach (var change in changes)
                    {
                        try { AxisStateChanged?.Invoke(this, change); }
                        catch (Exception ex) { logger.Error(ex, "轴状态事件处理异常: {Axis}", change.Name); }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { logger.Error(ex, "轴轮询异常"); }
            }
        }

        // ========== 运动控制 ==========

        public async Task<Result> MovePmoveAsync(AxisId axisId, double distance,
            ushort posiMode = 0,
            double? minVel = null,
            double? maxVel = null,
            double? tacc = null,
            double? tdec = null,
            double? stopVel = null,
            double? sPara = null,
            int timeoutMs = 0)
        {
            var axis = (ushort)axisId;
            var cfg = GetAxisConfig(axisId).Motion;
            var finalMinVel = minVel ?? cfg.MinVel;
            var finalMaxVel = maxVel ?? cfg.MaxVel;
            var finalTacc = tacc ?? cfg.Tacc;
            var finalTdec = tdec ?? cfg.Tdec;
            var finalStopVel = stopVel ?? cfg.StopVel;
            var finalSPara = sPara ?? cfg.SPara;

            try
            {
                if (!motionCard.IsConnected)
                    return Result.Fail("板卡未连接，请先初始化");

                // 检查并自动使能
                var smResult = await motionCard.GetAxisStateMachineAsync(axis);
                if (!smResult.IsSuccess)
                    return Result.Fail($"获取轴状态机失败: {smResult.Message}");

                if (smResult.Data != 4)
                {
                    logger.Warning("轴 {Axis} 未使能（状态机={SM}），尝试自动使能...", axis, smResult.Data);
                    var enableResult = await motionCard.EnableAxisAsync(axis);
                    if (!enableResult.IsSuccess)
                        return Result.Fail($"自动使能失败: {enableResult.Message}");
                }

                // 检查是否已有运动
                var doneResult = await motionCard.CheckDoneAsync(axis);
                if (!doneResult.IsSuccess) return Result.Fail($"检查运动状态失败: {doneResult.Message}");
                if (doneResult.Data == 0)
                    return Result.Fail($"轴 {axis} 正在运动中，请等待完成");

                // 检查硬限位
                var ioResult = await motionCard.GetAxisIoStatusAsync(axis);
                if (!ioResult.IsSuccess) return Result.Fail($"读取轴IO状态失败: {ioResult.Message}");
                var ioStatus = ioResult.Data;
                if ((ioStatus & 0x08) != 0) return Result.Fail("急停已触发，请复位急停按钮后再试");
                if ((ioStatus & 0x02) != 0) return Result.Fail($"轴 {axis} 正方向硬限位已触发");
                if ((ioStatus & 0x04) != 0) return Result.Fail($"轴 {axis} 负方向硬限位已触发");

                // 设置脉冲当量
                var equivResult = await motionCard.SetEquivAsync(axis, cfg.Equiv);
                if (!equivResult.IsSuccess) return Result.Fail($"设置脉冲当量失败: {equivResult.Message}");

                // 设置速度曲线
                var profileResult = await motionCard.SetProfileUnitAsync(axis, finalMinVel, finalMaxVel, finalTacc, finalTdec, finalStopVel);
                if (!profileResult.IsSuccess) return Result.Fail($"设置速度曲线失败: {profileResult.Message}");

                // S 段曲线
                if (finalSPara > 0)
                {
                    await motionCard.SetSProfileAsync(axis, 0, finalSPara);
                }

                logger.Information("轴 {Axis} 定长运动启动，距离={Dist}，速度={Vel}", axis, distance, finalMaxVel);

                // 启动运动
                var moveResult = await motionCard.PmoveUnitAsync(axis, distance, posiMode);
                if (!moveResult.IsSuccess) return Result.Fail($"启动定长运动失败: {moveResult.Message}");

                // 等待完成
                int elapsed = 0;
                int interval = 20;
                while (true)
                {
                    var checkResult = await motionCard.CheckDoneAsync(axis);
                    if (!checkResult.IsSuccess) return Result.Fail($"检查运动状态失败: {checkResult.Message}");
                    if (checkResult.Data == 1) break;

                    if (timeoutMs > 0 && elapsed >= timeoutMs)
                    {
                        await motionCard.StopAxisAsync(axis, false);
                        return Result.Fail($"轴 {axis} 运动超时 ({timeoutMs}ms)，已强制停止");
                    }
                    await Task.Delay(interval);
                    elapsed += interval;
                }

                logger.Information("轴 {Axis} 定长运动完成", axis);
                return Result.Success();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "轴 {Axis} 定长运动异常", axis);
                return Result.Fail($"运动异常: {ex.Message}");
            }
        }

        public async Task<Result> MoveHomeAsync(AxisId axisId,
            ushort? homeMode = null,
            double? lowVel = null,
            double? highVel = null,
            double? tacc = null,
            double? tdec = null,
            double? offsetPos = null,
            int timeoutMs = 30000)
        {
            var axis = (ushort)axisId;
            var cfg = GetAxisConfig(axisId).Home;
            var finalHomeMode = homeMode ?? cfg.HomeMode;
            var finalLowVel = lowVel ?? cfg.LowVel;
            var finalHighVel = highVel ?? cfg.HighVel;
            var finalTacc = tacc ?? cfg.Tacc;
            var finalTdec = tdec ?? cfg.Tdec;
            var finalOffsetPos = offsetPos ?? cfg.OffsetPos;

            try
            {
                if (!motionCard.IsConnected)
                    return Result.Fail("板卡未连接，请先初始化");

                // 检查并自动使能
                var smResult = await motionCard.GetAxisStateMachineAsync(axis);
                if (!smResult.IsSuccess)
                    return Result.Fail($"获取轴状态机失败: {smResult.Message}");

                if (smResult.Data != 4)
                {
                    logger.Warning("轴 {Axis} 未使能（状态机={SM}），尝试自动使能...", axis, smResult.Data);
                    var enableResult = await motionCard.EnableAxisAsync(axis);
                    if (!enableResult.IsSuccess)
                        return Result.Fail($"自动使能失败: {enableResult.Message}");
                }

                // 检查是否已有运动
                var doneResult = await motionCard.CheckDoneAsync(axis);
                if (!doneResult.IsSuccess) return Result.Fail($"检查运动状态失败: {doneResult.Message}");
                if (doneResult.Data == 0)
                    return Result.Fail($"轴 {axis} 正在运动中，无法回零");

                // 检查急停和限位
                var ioResult = await motionCard.GetAxisIoStatusAsync(axis);
                if (!ioResult.IsSuccess) return Result.Fail($"读取轴IO状态失败: {ioResult.Message}");
                var ioStatus = ioResult.Data;
                if ((ioStatus & 0x08) != 0) return Result.Fail("急停已触发，请复位急停按钮后再试");

                var isPositiveLimit = (ioStatus & 0x02) != 0;
                var isNegativeLimit = (ioStatus & 0x04) != 0;
                if (isPositiveLimit && isNegativeLimit)
                    return Result.Fail("正负限位同时触发，请检查限位传感器");

                // 设置回零参数
                var homeResult = await motionCard.SetHomeProfileAsync(axis, finalHomeMode, finalLowVel, finalHighVel, finalTacc, finalTdec, finalOffsetPos);
                if (!homeResult.IsSuccess) return Result.Fail($"设置回零参数失败: {homeResult.Message}");

                logger.Information("轴 {Axis} 回零启动，模式={Mode}", axis, finalHomeMode);

                // 启动回零
                var startResult = await motionCard.HomeMoveAsync(axis);
                if (!startResult.IsSuccess) return Result.Fail($"启动回零失败: {startResult.Message}");

                // 等待完成
                int elapsed = 0;
                int interval = 50;
                bool isCompleted = false;

                while (!isCompleted)
                {
                    if (timeoutMs > 0 && elapsed >= timeoutMs)
                    {
                        await motionCard.StopAxisAsync(axis, false);
                        return Result.Fail($"轴 {axis} 回零超时 ({timeoutMs}ms)，已强制停止");
                    }

                    var checkResult = await motionCard.CheckDoneAsync(axis);
                    if (!checkResult.IsSuccess) return Result.Fail($"检查运动状态失败: {checkResult.Message}");
                    if (checkResult.Data == 1)
                    {
                        isCompleted = true;
                        break;
                    }

                    if (elapsed % 500 == 0)
                    {
                        var currentIo = await motionCard.GetAxisIoStatusAsync(axis);
                        if (currentIo.IsSuccess && (currentIo.Data & 0x08) != 0)
                        {
                            await motionCard.StopAxisAsync(axis, true);
                            return Result.Fail("回零过程中急停被触发");
                        }
                    }

                    await Task.Delay(interval);
                    elapsed += interval;
                }

                // 读取回零结果
                var result = await motionCard.GetHomeResultAsync(axis);
                if (!result.IsSuccess) return Result.Fail($"读取回零结果失败: {result.Message}");

                if (result.Data == 1)
                {
                    logger.Information("轴 {Axis} 回零成功", axis);
                    return Result.Success();
                }
                else
                {
                    var reasonResult = await motionCard.GetStopReasonAsync(axis);
                    var reason = reasonResult.IsSuccess ? GetStopReasonDescription(reasonResult.Data) : "未知";
                    logger.Warning("轴 {Axis} 回零失败，停止原因: {Reason}", axis, reason);
                    return Result.Fail($"轴 {axis} 回零失败，停止原因: {reason}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "轴 {Axis} 回零异常", axis);
                return Result.Fail($"回零异常: {ex.Message}");
            }
        }

        public async Task<Result> StopAxisAsync(AxisId axisId, bool emergency = false)
        {
            return await motionCard.StopAxisAsync((ushort)axisId, emergency);
        }

        public async Task<Result> EmergencyStopAllAsync()
        {
            return await motionCard.EmergencyStopAllAsync();
        }

        public async Task<Result> MoveLineAsync(AxisId[] axisList, double[] targetPositions,
            ushort posiMode = 0,
            double? minVel = null,
            double? maxVel = null,
            double? tacc = null,
            double? tdec = null,
            double? stopVel = null,
            double? sPara = null,
            int timeoutMs = 0)
        {
            ushort crd = 0;
            try
            {
                if (!motionCard.IsConnected)
                    return Result.Fail("板卡未连接，请先初始化");

                if (axisList == null || axisList.Length < 2)
                    return Result.Fail("插补至少需要2个轴");

                if (targetPositions == null || targetPositions.Length != axisList.Length)
                    return Result.Fail("目标位置数组长度必须与轴列表长度一致");

                int axisCount = axisList.Length;
                var rawAxisList = new ushort[axisCount];
                var equivList = new double[axisCount];

                for (int i = 0; i < axisCount; i++)
                {
                    rawAxisList[i] = (ushort)axisList[i];
                    equivList[i] = GetAxisConfig(axisList[i]).Motion.Equiv;

                    // 检查使能
                    var smResult = await motionCard.GetAxisStateMachineAsync(rawAxisList[i]);
                    if (!smResult.IsSuccess)
                        return Result.Fail($"获取轴 {rawAxisList[i]} 状态机失败: {smResult.Message}");

                    if (smResult.Data != 4)
                    {
                        var enableResult = await motionCard.EnableAxisAsync(rawAxisList[i]);
                        if (!enableResult.IsSuccess)
                            return Result.Fail($"轴 {rawAxisList[i]} 自动使能失败: {enableResult.Message}");
                    }
                }

                // 检查坐标系空闲
                var multiDone = await motionCard.CheckDoneMultiCoorAsync(crd);
                if (!multiDone.IsSuccess) return Result.Fail($"检查坐标系状态失败: {multiDone.Message}");
                if (multiDone.Data == 0)
                    return Result.Fail($"坐标系 {crd} 正在运动中，请等待完成");

                // 设置脉冲当量
                for (int i = 0; i < axisCount; i++)
                {
                    var eqResult = await motionCard.SetEquivAsync(rawAxisList[i], equivList[i]);
                    if (!eqResult.IsSuccess)
                        return Result.Fail($"设置轴 {rawAxisList[i]} 脉冲当量失败: {eqResult.Message}");
                }

                // 检查限位
                for (int i = 0; i < axisCount; i++)
                {
                    var ioResult = await motionCard.GetAxisIoStatusAsync(rawAxisList[i]);
                    if (!ioResult.IsSuccess) return Result.Fail($"读取轴 {rawAxisList[i]} IO状态失败: {ioResult.Message}");
                    var ioStatus = ioResult.Data;
                    if ((ioStatus & 0x08) != 0) return Result.Fail($"轴 {rawAxisList[i]} 急停已触发");
                    if ((ioStatus & 0x02) != 0) return Result.Fail($"轴 {rawAxisList[i]} 正方向硬限位已触发");
                    if ((ioStatus & 0x04) != 0) return Result.Fail($"轴 {rawAxisList[i]} 负方向硬限位已触发");
                }

                // 获取速度参数（使用第一个轴的配置）
                var firstCfg = GetAxisConfig(axisList[0]).Motion;
                var finalMinVel = minVel ?? firstCfg.MinVel;
                var finalMaxVel = maxVel ?? firstCfg.MaxVel;
                var finalTacc = tacc ?? firstCfg.Tacc;
                var finalTdec = tdec ?? firstCfg.Tdec;
                var finalStopVel = stopVel ?? firstCfg.StopVel;
                var finalSPara = sPara ?? firstCfg.SPara;

                var vpResult = await motionCard.SetVectorProfileUnitAsync(crd, finalMinVel, finalMaxVel, finalTacc, finalTdec, finalStopVel);
                if (!vpResult.IsSuccess) return Result.Fail($"设置插补速度曲线失败: {vpResult.Message}");

                if (finalSPara > 0)
                    await motionCard.SetVectorSProfileAsync(crd, 0, finalSPara);

                logger.Information("坐标系 {Crd} 直线插补启动", crd);

                var lineResult = await motionCard.LineUnitAsync(crd, (ushort)axisCount, rawAxisList, targetPositions, posiMode);
                if (!lineResult.IsSuccess) return Result.Fail($"启动直线插补失败: {lineResult.Message}");

                // 等待完成
                int elapsed = 0;
                int interval = 20;
                while (true)
                {
                    var checkResult = await motionCard.CheckDoneMultiCoorAsync(crd);
                    if (!checkResult.IsSuccess) return Result.Fail($"检查插补状态失败: {checkResult.Message}");
                    if (checkResult.Data == 1) break;

                    if (timeoutMs > 0 && elapsed >= timeoutMs)
                    {
                        await motionCard.StopMultiCoorAsync(crd, 0);
                        return Result.Fail($"坐标系 {crd} 插补超时 ({timeoutMs}ms)，已强制停止");
                    }
                    await Task.Delay(interval);
                    elapsed += interval;
                }

                logger.Information("坐标系 {Crd} 直线插补完成", crd);
                return Result.Success();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "坐标系 {Crd} 直线插补异常", crd);
                return Result.Fail($"插补异常: {ex.Message}");
            }
        }

        public async Task<Result<double>> GetPositionAsync(AxisId axisId)
        {
            return await motionCard.GetPositionAsync((ushort)axisId);
        }

        public async Task<Result<double>> GetSpeedAsync(AxisId axisId)
        {
            return await motionCard.GetSpeedAsync((ushort)axisId);
        }

        public async Task<Result> SetSoftLimitAsync(AxisId axisId)
        {
            var cfg = GetAxisConfig(axisId);
            return await motionCard.SetSoftLimitAsync((ushort)axisId, cfg.NegativeSoftLimit, cfg.PositiveSoftLimit, cfg.SoftLimitEnabled);
        }

        public async Task<Result> EnableAxisAsync(AxisId axisId, int timeoutMs = 3000)
        {
            return await motionCard.EnableAxisAsync((ushort)axisId, timeoutMs);
        }

        public Result DisableAxis(AxisId axisId)
        {
            return motionCard.DisableAxis((ushort)axisId);
        }

        // ========== 辅助 ==========

        public static string GetAxisDisplayName(AxisId id)
        {
            return id switch
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
        }

        private static string GetStopReasonDescription(long reason)
        {
            return reason switch
            {
                0 => "正常停止",
                1 => "ALM 立即停止",
                2 => "ALM 减速停止",
                3 => "LTC 外部触发立即停止",
                4 => "EMG 立即停止",
                5 => "正硬限位立即停止",
                6 => "负硬限位立即停止",
                7 => "正硬限位减速停止",
                8 => "负硬限位减速停止",
                9 => "正软限位立即停止",
                10 => "负软限位立即停止",
                11 => "正软限位减速停止",
                12 => "负软限位减速停止",
                13 => "命令立即停止",
                14 => "命令减速停止",
                19 => "DSTP 信号引起的减速停止",
                21 => "原点不在两个限位之间",
                22 => "回零方向与限位方向冲突",
                23 => "正负限位同时有效",
                24 => "没有找到EZ信号",
                25 => "回零位置溢出",
                201 => "正负限位之间全程没找到原点信号",
                202 => "回零方向不匹配",
                203 => "正负限位同时有效",
                204 => "正负限位之间全程没有EZ信号",
                205 => "位置溢出",
                206 => "双原点错误",
                207 => "外部信号触发回零停止",
                208 => "驱动器回零被中断停止",
                _ => $"未知原因 (code: {reason})"
            };
        }

     
    }
}
