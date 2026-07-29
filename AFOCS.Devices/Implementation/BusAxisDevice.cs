using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation;

[Export]
[Export(typeof(IBusAxisDevice))]
[method: ImportingConstructor]
public class BusAxisDevice(IMotionControlCard motionCard, IConfigService configService, ILogger logger) : IBusAxisDevice
{
    private readonly Dictionary<BusAxisId, AxisConfig> _axisConfigs = [];
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
            await StartAxisMonitorAsync();
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

    #region 轴配置管理
    public AxisConfig GetAxisConfig(BusAxisId busAxisId)
    {
        if (_axisConfigs.TryGetValue(busAxisId, out var config))
            return config;

        var defaults = GetDefaultAxisConfig(busAxisId);
        _axisConfigs[busAxisId] = defaults;
        return defaults;
    }

    public void SetAxisConfig(BusAxisId busAxisId, AxisConfig config)
    {
        _axisConfigs[busAxisId] = config.Clone();
    }

    public async Task LoadAllAxisConfigsAsync()
    {
        try
        {
            var collection = await configService.LoadAsync<AxisConfigCollection>();
            if (collection?.Axes != null)
            {
                foreach (var (key, config) in collection.Axes)
                    _axisConfigs[(BusAxisId)key] = config;
            }

            foreach (BusAxisId axisId in Enum.GetValues<BusAxisId>())
            {
                if (!_axisConfigs.ContainsKey(axisId))
                    _axisConfigs[axisId] = GetDefaultAxisConfig(axisId);
            }

            logger.Information("轴配置加载完成，共 {Count} 个轴", _axisConfigs.Count);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "加载轴配置失败");
            foreach (BusAxisId axisId in Enum.GetValues<BusAxisId>())
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

    public AxisConfig GetDefaultAxisConfig(BusAxisId busAxisId)
    {
        var config = new AxisConfig { BusAxisId = busAxisId };

        switch (busAxisId)
        {
            case BusAxisId.LeftCamUpX:
            case BusAxisId.RightCamUpX:
                config.Motion.Equiv = 8388608 / (5.0 * 100); // 10um
                config.Motion.MinVel = 200;
                config.Motion.MaxVel = 500;
                break;
            case BusAxisId.LeftCamUpY:
            case BusAxisId.RightCamUpY:
                config.Motion.Equiv = 8388608 / (10.0 * 100); // 10um
                config.Motion.MinVel = 200;
                config.Motion.MaxVel = 500;
                break;
            case BusAxisId.LeftCamUpZ:
            case BusAxisId.RightCamUpZ:
                config.Motion.Equiv = 8388608 / (1.0 * 100);// 10um
                config.Motion.MinVel = 200;
                config.Motion.MinVel = 500;
                break;
            case BusAxisId.LeftCamSideY:
            case BusAxisId.RightCamSideY:
                config.Motion.Equiv = 8388608 / (1.0 * 100);// 10um
                config.Motion.MinVel = 200;
                config.Motion.MinVel = 500;
                break;

            case BusAxisId.LeftCouplingLThetaX:
            case BusAxisId.RightCouplingLThetaX:
            case BusAxisId.LeftCouplingRThetaX:
            case BusAxisId.RightCouplingRThetaX:
                config.Motion.Equiv = 50000 / 1.0324;
                config.Motion.MinVel = 0.5;
                config.Motion.MinVel = 1;
                break;

            case BusAxisId.LeftCouplingLThetaY:
            case BusAxisId.LeftCouplingRThetaY:
            case BusAxisId.RightCouplingLThetaY:
            case BusAxisId.RightCouplingRThetaY:
                config.Motion.Equiv = 50000 / 1.0324;
                config.Motion.MinVel = 0.5;
                config.Motion.MinVel = 5;
                break;
            case BusAxisId.LeftCouplingRThetaZ:
            case BusAxisId.LeftCouplingLThetaZ:
            case BusAxisId.RightCouplingLThetaZ:
            case BusAxisId.RightCouplingRThetaZ:
                config.Motion.Equiv = 50000 / 1.8789;
                config.Motion.MinVel = 0.5;
                config.Motion.MinVel = 5;
                break;
        }

        return config;
    }

    #endregion

    #region 轴状态监控


    public event EventHandler<BusAxisStateChangedEventArgs>? AxisStateChanged;
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

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(intervalMs, ct);

                if (!motionCard.IsConnected) continue;

                var changes = new List<BusAxisStateChangedEventArgs>();

                foreach (BusAxisId id in Enum.GetValues<BusAxisId>())
                {
                    ct.ThrowIfCancellationRequested();

                    var posResult = await motionCard.GetPositionAsync((ushort)id);
                    var pos = posResult.IsSuccess ? posResult.Data : -999999;

                    var speedResult = await motionCard.GetSpeedAsync((ushort)id);
                    var speed = speedResult.IsSuccess ? speedResult.Data : -999999;

                    var ioResult = await motionCard.GetAxisIoStatusAsync((ushort)id);
                    var ioStatus = ioResult.IsSuccess ? ioResult.Data : 0;

                    var smResult = await motionCard.GetAxisStateMachineAsync((ushort)id);
                    var stateMachine = smResult.IsSuccess ? smResult.Data : (ushort)0;

                    var name = GetAxisDisplayName(id);
                    changes.Add(new BusAxisStateChangedEventArgs(
                        id, name,
                        pos, speed,
                        ioStatus, stateMachine));
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

    #endregion
    // ========== 运动控制 ==========

    public async Task<Result> MovePmoveAsync(BusAxisId busAxisId, double distance,
        ushort posiMode = 0,
        double? minVel = null,
        double? maxVel = null,
        double? tacc = null,
        double? tdec = null,
        double? stopVel = null,
        double? sPara = null,
        int timeoutMs = 0)
    {
        var axis = (ushort)busAxisId;
        var cfg = GetAxisConfig(busAxisId).Motion;
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
            if (!doneResult.IsSuccess) return Result.Fail($"当前有轴在运动了");
            if (doneResult.Data == 0)
                return Result.Fail($"轴 {axis} 正在运动中，请等待完成");

            // 检查硬限位（方向感知：正限位只阻止正向移动，负限位只阻止负向移动）
            var ioResult = await motionCard.GetAxisIoStatusAsync(axis);
            if (!ioResult.IsSuccess) return Result.Fail($"读取轴IO状态失败: {ioResult.Message}");
            var ioStatus = ioResult.Data;
            //if ((ioStatus & 0x08) != 0) return Result.Fail("急停已触发，请复位急停按钮后再试");
            //if ((ioStatus & 0x01) != 0) return Result.Fail($"轴 {axis} 驱动器报警，请检查驱动器状态");
            //if ((ioStatus & 0x02) != 0 && distance > 0)
            //    return Result.Fail($"轴 {axis} 已触发正限位，无法继续正向移动，请先反向移动脱困");
            //if ((ioStatus & 0x04) != 0 && distance < 0)
            //    return Result.Fail($"轴 {axis} 已触发负限位，无法继续负向移动，请先反向移动脱困");

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

    public async Task<Result> MoveHomeAsync(BusAxisId busAxisId,
        ushort? homeMode = null,
        double? lowVel = null,
        double? highVel = null,
        double? tacc = null,
        double? tdec = null,
        double? offsetPos = null,
        int timeoutMs = 30000)
    {
        var axis = (ushort)busAxisId;
        var axisConfig = GetAxisConfig(busAxisId);
        var cfg = axisConfig.Home;
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
            //if ((ioStatus & 0x08) != 0) return Result.Fail("急停已触发，请复位急停按钮后再试");
            //if ((ioStatus & 0x01) != 0) return Result.Fail($"轴 {axis} 驱动器报警，请检查驱动器状态");

            //var isPositiveLimit = (ioStatus & 0x02) != 0;
            //var isNegativeLimit = (ioStatus & 0x04) != 0;
            //if (isPositiveLimit && isNegativeLimit)
            //    return Result.Fail("正负限位同时触发，请检查限位传感器");
            //if (isPositiveLimit)
            //    logger.Warning("轴 {Axis} 正限位已触发，回零将向负方向寻原点", axis);
            //if (isNegativeLimit)
            //    logger.Warning("轴 {Axis} 负限位已触发，回零将向正方向寻原点", axis);

            // 设置脉冲当量
            var equivResult = await motionCard.SetEquivAsync(axis, axisConfig.Motion.Equiv);
            if (!equivResult.IsSuccess) return Result.Fail($"设置脉冲当量失败: {equivResult.Message}");

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

    public async Task<Result> StopAxisAsync(BusAxisId busAxisId, bool emergency = false)
    {
        return await motionCard.StopAxisAsync((ushort)busAxisId, emergency);
    }

    public async Task<Result> EmergencyStopAllAsync()
    {
        return await motionCard.EmergencyStopAllAsync();
    }

    public async Task<Result> MoveLineAsync(BusAxisId[] axisList, double[] targetPositions,
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

            if (axisList.Length < 2)
                return Result.Fail("插补至少需要2个轴");

            if (targetPositions.Length != axisList.Length)
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

            // 检查限位（方向感知：正限位只阻止正向移动，负限位只阻止负向移动）
            for (int i = 0; i < axisCount; i++)
            {
                var ioResult = await motionCard.GetAxisIoStatusAsync(rawAxisList[i]);
                if (!ioResult.IsSuccess) return Result.Fail($"读取轴 {rawAxisList[i]} IO状态失败: {ioResult.Message}");
                var ioStatus = ioResult.Data;
                //if ((ioStatus & 0x08) != 0) return Result.Fail($"轴 {rawAxisList[i]} 急停已触发");
                //if ((ioStatus & 0x01) != 0) return Result.Fail($"轴 {rawAxisList[i]} 驱动器报警");

                // 获取当前位置判断移动方向
                var posRes = await motionCard.GetPositionAsync(rawAxisList[i]);
                if (!posRes.IsSuccess) return Result.Fail($"读取轴 {rawAxisList[i]} 位置失败: {posRes.Message}");
                var delta = targetPositions[i] - posRes.Data;

                if ((ioStatus & 0x02) != 0 && delta > 0)
                    return Result.Fail($"轴 {rawAxisList[i]} 已触发正限位，目标方向为正向，无法插补");
                if ((ioStatus & 0x04) != 0 && delta < 0)
                    return Result.Fail($"轴 {rawAxisList[i]} 已触发负限位，目标方向为负向，无法插补");
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

    public async Task<Result<double>> GetPositionAsync(BusAxisId busAxisId)
    {
        return await motionCard.GetPositionAsync((ushort)busAxisId);
    }

    public async Task<Result<double>> GetSpeedAsync(BusAxisId busAxisId)
    {
        return await motionCard.GetSpeedAsync((ushort)busAxisId);
    }

    public async Task<Result> SetSoftLimitAsync(BusAxisId busAxisId)
    {
        var cfg = GetAxisConfig(busAxisId);
        return await motionCard.SetSoftLimitAsync((ushort)busAxisId, cfg.NegativeSoftLimit, cfg.PositiveSoftLimit, cfg.SoftLimitEnabled);
    }

    public async Task<Result> EnableAxisAsync(BusAxisId busAxisId, int timeoutMs = 3000)
    {
        return await motionCard.EnableAxisAsync((ushort)busAxisId, timeoutMs);
    }

    public Result DisableAxis(BusAxisId busAxisId)
    {
        return motionCard.DisableAxis((ushort)busAxisId);
    }

    // ========== 辅助 ==========

    public static string GetAxisDisplayName(BusAxisId id)
    {
        return id switch
        {
            BusAxisId.LeftCamUpX => "左上相机X轴",
            BusAxisId.LeftCamUpY => "左上相机Y轴",
            BusAxisId.LeftCamUpZ => "左上相机Z轴",
            BusAxisId.LeftCamSideY => "左侧相机Y轴",
            BusAxisId.LeftCouplingLThetaX => "左耦合左θX轴",
            BusAxisId.LeftCouplingLThetaY => "左耦合左θY轴",
            BusAxisId.LeftCouplingLThetaZ => "左耦合左θZ轴",
            BusAxisId.LeftCouplingRThetaX => "左耦合右θX轴",
            BusAxisId.LeftCouplingRThetaY => "左耦合右θY轴",
            BusAxisId.LeftCouplingRThetaZ => "左耦合右θZ轴",
            BusAxisId.RightCamUpX => "右上相机X轴",
            BusAxisId.RightCamUpY => "右上相机Y轴",
            BusAxisId.RightCamUpZ => "右上相机Z轴",
            BusAxisId.RightCamSideY => "右侧相机Y轴",
            BusAxisId.RightCouplingLThetaX => "右耦合左θX轴",
            BusAxisId.RightCouplingLThetaY => "右耦合左θY轴",
            BusAxisId.RightCouplingLThetaZ => "右耦合左θZ轴",
            BusAxisId.RightCouplingRThetaX => "右耦合右θX轴",
            BusAxisId.RightCouplingRThetaY => "右耦合右θY轴",
            BusAxisId.RightCouplingRThetaZ => "右耦合右θZ轴",
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

/// <summary>轴状态变化事件参数</summary>
public class BusAxisStateChangedEventArgs : EventArgs
{
    public BusAxisId BusAxisId { get; }
    public string Name { get; }
    public double Position { get; }
    public double Speed { get; }
    public DateTime Timestamp { get; } = DateTime.Now;

    // ========== IO 状态 ==========

    /// <summary>原始 IO 状态值（位掩码）</summary>
    public uint IoStatusRaw { get; }

    /// <summary>报警信号（ALM）</summary>
    public bool IsAlarm { get; }

    /// <summary>正方向硬限位（PEL）</summary>
    public bool IsPositiveLimit { get; }

    /// <summary>负方向硬限位（NEL）</summary>
    public bool IsNegativeLimit { get; }

    /// <summary>急停信号（EMG）</summary>
    public bool IsEmergencyStop { get; }

    // ========== 轴状态 ==========

    /// <summary>轴状态机值</summary>
    public ushort StateMachine { get; }

    /// <summary>轴是否已使能（状态机 == 4 表示 Operation Enabled）</summary>
    public bool IsEnabled => StateMachine == 4;

    public BusAxisStateChangedEventArgs(
        BusAxisId busAxisId,
        string name,
        double position,
        double speed,
        uint ioStatusRaw,
        ushort stateMachine)
    {
        BusAxisId = busAxisId;
        Name = name;
        Position = position;
        Speed = speed;
        IoStatusRaw = ioStatusRaw;
        StateMachine = stateMachine;

        // 解析 IO 状态位
        // bit0: ALM (报警), bit1: PEL (正限位), bit2: NEL (负限位), bit3: EMG (急停)
        IsAlarm = (ioStatusRaw & 0x01) != 0;
        IsPositiveLimit = (ioStatusRaw & 0x02) != 0;
        IsNegativeLimit = (ioStatusRaw & 0x04) != 0;
        IsEmergencyStop = (ioStatusRaw & 0x08) != 0;
    }
}