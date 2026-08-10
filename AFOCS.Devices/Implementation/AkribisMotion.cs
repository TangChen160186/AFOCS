using AAMotion;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation;

public abstract class AkribisMotion : IAkribisMotion
{
    private readonly ILogger _logger;
    private readonly IConfigService _configService;
    private readonly MotionController _controller;
    private AkribisCouplingConfig _config = new();

    private  const int DefaultEquip = 2048; // 本来是204800
    public bool IsConnected => _controller.IsConnected;

    /// <summary>子类覆写：返回专用的配置类型（用于 ConfigService 区分配置文件）</summary>
    protected abstract Type ConfigType { get; }


    protected AkribisMotion(IConfigService configService, ILogger logger)
    {
        _configService = configService;
        _logger = logger;
        _controller = AAMotionAPI.Initialize(ControllerType.AGD301);
    }

    // ========== 配置 ==========

    public AkribisCouplingConfig GetConfig() => _config.Clone();

    AkribisAxisParams IAkribisMotion.GetAxisParams(AkribisAxisId axis) => axis switch
    {
        AkribisAxisId.X => _config.XAxis,
        AkribisAxisId.Y => _config.YAxis,
        AkribisAxisId.Z => _config.ZAxis,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };

    protected AkribisAxisParams GetAxisParams(AkribisAxisId axis) => axis switch
    {
        AkribisAxisId.X => _config.XAxis,
        AkribisAxisId.Y => _config.YAxis,
        AkribisAxisId.Z => _config.ZAxis,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };

    public async Task SaveConfigAsync(AkribisCouplingConfig config)
    {
        _config = config.Clone();
        await _configService.SaveAsync(ConfigType, _config);
    }

    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        var loaded = await _configService.LoadAsync(ConfigType);
        if (loaded is AkribisCouplingConfig config)
        {
            _config = config;
        }
        else
        {
            _config = (AkribisCouplingConfig)Activator.CreateInstance(ConfigType)!;
            await _configService.SaveAsync(ConfigType, _config); ;
        }

        var connectTask = Task.Run(() => AAMotionAPI.Connect(_controller, _config.Ip, _config.Ark, _config.AutoReconnect), token);
        var success = await connectTask.ConfigureAwait(false);
        if (!success)
            return Result.Fail(ResultCode.Fail, $"链接失败, IP: {_config.Ip}");

        _controller.ErrorOccurred += OnControllerErrorOccurred;

        var readyA = EnsureAxisReady(AxisRef.A);
        var readyB = EnsureAxisReady(AxisRef.B);
        var readyC = EnsureAxisReady(AxisRef.C);
        if (!readyA || !readyB || !readyC)
            return Result.Fail(ResultCode.Fail, "使能或者换向失败");

        _logger.Information("[{Type}] 初始化成功, IP={Ip}", ConfigType.Name, _config.Ip);

        StartMonitoring();
        return Result.Success();
    }

    // ========== 使能 ==========

    public async Task<Result> EnableAsync(AkribisAxisId axis)
    {
        if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
        var ar = AxisConverter(axis);
        AAMotionAPI.MotorOn(_controller, ar);
        return _controller.GetAxis(ar).MotorOn == 1
            ? Result.Success("使能成功")
            : Result.Fail("使能失败");
    }

    public async Task<Result> DisEnableAsync(AkribisAxisId axis)
    {
        if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
        var ar = AxisConverter(axis);
        AAMotionAPI.MotorOff(_controller, ar);
        return _controller.GetAxis(ar).MotorOn == 0
            ? Result.Success("断电成功")
            : Result.Fail("断电失败");
    }

    // ========== 回零 ==========

    public async Task<Result> HomeAsync(AkribisAxisId axis,int timeoutMs = 0)
    {
        if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
        var ar = AxisConverter(axis);
        if (!EnsureAxisReady(ar)) return Result.Fail(ResultCode.Fail, "换相或使能失败");

        AAMotionAPI.Home(_controller, ar);

        int elapsed = 0, interval = 20;
        while (true)
        {
            var homingStat = _controller.GetAxis(ar).HomingStat;
            
            if (_controller.GetAxis(ar).IsHomed())
            {
                _logger.Information("[{Type}] 轴 {Axis} 回零成功", ConfigType.Name, axis);
                return Result.Success();
            }
            if (timeoutMs > 0 && elapsed >= timeoutMs)
            {
                await StopAxisAsync(axis);
                var homeFailReason = MapHomingError(homingStat);
                _logger.Error($"[{ConfigType.Name}] 轴 {axis} 回零失败, 错误码: {homingStat},错误原因:{homeFailReason}");
                return Result.Fail(homeFailReason);

            }
            await Task.Delay(interval);
            elapsed += interval;
        }
    }

    // ========== 相对运动 ==========

    public async Task<Result> MoveRelativeAsync(AkribisAxisId axis, int distance,
        int? speed = null, int? accel = null, int? decel = null, int timeoutMs = 0)
    {
        if (!IsConnected) return Result.Fail("未连接设备");
        var ar = AxisConverter(axis);
        if (!EnsureAxisReady(ar)) return Result.Fail(ResultCode.Fail, "换相或使能失败");

        var p = GetAxisParams(axis);
        var s = (speed ?? p.Speed) /** DefaultEquip*/;
        var a = (accel ?? p.Accel) /** DefaultEquip*/;
        var d = (decel ?? p.Decel) /** DefaultEquip*/;
        //distance *= DefaultEquip; 
        if (!AAMotionAPI.MoveRel(_controller, a, s, d, [_controller.GetAxis(ar)], [distance]))
            return Result.Fail(ResultCode.Fail, "相对运动指令发送失败");

        return await WaitForMotionDone(ar, axis, timeoutMs);
    }

    // ========== 绝对运动 ==========

    public async Task<Result> MoveAbsAsync(AkribisAxisId axis, int position,
        int? speed = null, int? accel = null, int? decel = null, int timeoutMs = 0)
    {
        if (!IsConnected) return Result.Fail("未连接设备");
        var ar = AxisConverter(axis);
        if (!EnsureAxisReady(ar)) return Result.Fail(ResultCode.Fail, "换相或使能失败");

        var p = GetAxisParams(axis);
        var s = (speed ?? p.Speed)/* * DefaultEquip*/;
        var a = (accel ?? p.Accel)/* * DefaultEquip*/;
        var d = (decel ?? p.Decel)/* * DefaultEquip*/;
        //position *= DefaultEquip;
        AAMotionAPI.MoveAbs(_controller, ar, position, s, a, d);

        return await WaitForMotionDone(ar, axis, timeoutMs);
    }

    // ========== 多轴直线插补 ==========

    public async Task<Result> MoveLineRelativeAsync(AkribisAxisId[] axiss, int[] distances,
        int? speed = null, int? accel = null, int? decel = null, int timeoutMs = 0)
    {
        if (!IsConnected) return Result.Fail("未连接设备");

        var ars = new List<AxisRef>();
        foreach (var axis in axiss)
        {
            var ar = AxisConverter(axis);
            ars.Add(ar);
            if (!EnsureAxisReady(ar)) return Result.Fail(ResultCode.Fail, "换相或使能失败");
        }

        var p = GetAxisParams(axiss[0]);
        var s = (speed ?? p.Speed) /** DefaultEquip*/;
        var a = (accel ?? p.Accel) /** DefaultEquip*/;
        var d = (decel ?? p.Decel) /** DefaultEquip*/;

        if (!AAMotionAPI.MoveRel(_controller, a, s, d,
                ars.Select(e => _controller.GetAxis(e)).ToArray(), distances.Select(e=>e /** DefaultEquip*/).ToArray()))
            return Result.Fail(ResultCode.Fail, "直线插补指令发送失败");

        int elapsed = 0, interval = 20;
        while (true)
        {
            if (ars.All(ar => _controller.GetAxis(ar).MotionStat == 0)) return Result.Success();
            if (timeoutMs > 0 && elapsed >= timeoutMs)
            {
                await StopAxisAsync();
                return Result.Fail($"轴 {string.Join(",", axiss)} 运动超时 ({timeoutMs}ms)，已强制停止");
            }
            await Task.Delay(interval);
            elapsed += interval;
        }
    }

    // ========== 后台轮询 ==========

    private CancellationTokenSource? _pollCts;
    private readonly Lock _posLock = new();
    private int _posX, _posY, _posZ;

    public bool IsMonitoring { get; private set; }
    public event EventHandler<AkribisPositionChangedEventArgs>? PositionChanged;

    public int PositionX { get { lock (_posLock) return _posX; } }
    public int PositionY { get { lock (_posLock) return _posY; } }
    public int PositionZ { get { lock (_posLock) return _posZ; } }

    private void StartMonitoring(int intervalMs = 100)
    {
        if (IsMonitoring) return;
        _pollCts = new CancellationTokenSource();
        IsMonitoring = true;
        _logger.Information("[{Type}] 后台轮询已启动，间隔 {Interval}ms", ConfigType.Name, intervalMs);
        _ = Task.Run(() => PollLoopAsync(intervalMs, _pollCts.Token), _pollCts.Token);
    }

    private void StopMonitoring()
    {
        if (!IsMonitoring) return;
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
        IsMonitoring = false;
        _logger.Information("[{Type}] 后台轮询已停止", ConfigType.Name);
    }

    private async Task PollLoopAsync(int intervalMs, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(intervalMs, ct);

                if (!IsConnected) continue;

                var axisA = _controller.GetAxis(AxisRef.A);
                var axisB = _controller.GetAxis(AxisRef.B);
                var axisC = _controller.GetAxis(AxisRef.C);

                int newX = (int)axisA.Pos /*/ DefaultEquip*/;
                int newY = (int)axisB.Pos /*/ DefaultEquip*/;
                int newZ = (int)axisC.Pos /*/ DefaultEquip*/;

                bool changed;
                lock (_posLock)
                {
                    changed = newX != _posX || newY != _posY || newZ != _posZ;
                    _posX = newX;
                    _posY = newY;
                    _posZ = newZ;
                }

                if (changed)
                    PositionChanged?.Invoke(this, new AkribisPositionChangedEventArgs(newX, newY, newZ));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("[{Type}] 轮询异常: {Msg}", ConfigType.Name, ex.Message);
            }
        }
    }

    // ========== 停止 ==========

    public async Task<Result> StopAxisAsync(AkribisAxisId axis)
    {
        if (!IsConnected) return Result.Fail("未连接设备");
        var ar = AxisConverter(axis);
        if (!EnsureAxisReady(ar)) return Result.Fail(ResultCode.Fail, "换相或使能失败");
        return AAMotionAPI.Stop(_controller, ar)
            ? Result.Success()
            : Result.Fail($"停止轴:{axis}失败");
    }

    public async Task<Result> StopAxisAsync()
    {
        if (!IsConnected) return Result.Fail("未连接设备");
        var results = await Task.WhenAll(
            Enum.GetValues<AkribisAxisId>().Select(StopAxisAsync));
        if (results.All(r => r.IsSuccess)) return Result.Success();
        var failed = results.Where(r => !r.IsSuccess).Select(r => r.Message);
        return Result.Fail($"停止所有轴时部分失败：{string.Join("; ", failed)}");
    }

    public async Task<Result> EmergencyStopAsync(AkribisAxisId axis)
    {
        if (!IsConnected) return Result.Fail("未连接设备");
        var ar = AxisConverter(axis);
        if (!EnsureAxisReady(ar)) return Result.Fail(ResultCode.Fail, "换相或使能失败");
        return AAMotionAPI.Abort(_controller, ar)
            ? Result.Success()
            : Result.Fail($"紧急停止轴:{axis}失败");
    }

    public async Task<Result> EmergencyStopAllAsync()
    {
        if (!IsConnected) return Result.Fail("未连接设备");
        var results = await Task.WhenAll(
            Enum.GetValues<AkribisAxisId>().Select(EmergencyStopAsync));
        if (results.All(r => r.IsSuccess)) return Result.Success();
        var failed = results.Where(r => !r.IsSuccess).Select(r => r.Message);
        return Result.Fail($"紧急停止所有轴时部分失败：{string.Join("; ", failed)}");
    }

    // ========== Device 生命周期 ==========

    public async Task<Result> StopAsync(CancellationToken token = default)
    {
        StopMonitoring();

        if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
        return _controller.Disconnect()
            ? Result.Success("成功断开连接")
            : Result.Fail(ResultCode.Fail, "断开连接失败");
    }

    public async Task<Result> ReConnectAsync(CancellationToken token = default)
    {
        if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
        return _controller.TryReconnect(_config.Ip, _config.Ark)
            ? Result.Success("重连成功")
            : Result.Fail(ResultCode.Fail, "重连失败");
    }

    public void Dispose()
    {
        StopMonitoring();
        _controller.Dispose();
    }

    // ========== 内部辅助 ==========

    private async Task<Result> WaitForMotionDone(AxisRef ar, AkribisAxisId axis, int timeoutMs)
    {
        int elapsed = 0, interval = 20;
        while (true)
        {
            if (_controller.GetAxis(ar).MotionStat == 0) return Result.Success();
            if (timeoutMs > 0 && elapsed >= timeoutMs)
            {
                await StopAxisAsync(axis);
                return Result.Fail($"轴 {axis} 运动超时 ({timeoutMs}ms)");

            }
            await Task.Delay(interval);
            elapsed += interval;
        }
    }

    private bool EnsureAxisReady(AxisRef axis)
    {
        //if (!_controller.GetAxis(axis).IsCommutated())
        //{
        //    _logger.Information("[{Type}] 换向未完成，正在执行 AutoPhase...", ConfigType.Name);
        //    AAMotionAPI.AutoPhase(_controller, axis, 5000);
        //    if (!_controller.GetAxis(axis).IsCommutated()) return false;
        //}
        if (_controller.GetAxis(axis).MotorOn == 0)
        {
            _logger.Information("[{Type}] 电机未使能，正在使能...", ConfigType.Name);
            AAMotionAPI.MotorOn(_controller, axis);
            Thread.Sleep(100);
            return _controller.GetAxis(axis).MotorOn == 1;
        }
        return true;
    }

    private void OnControllerErrorOccurred(int errorCode, string msgSent, string errorMsg)
    {
        _logger.Error("[{Type}] 控制器错误 - 发送: {Msg}, 错误码: {Code}, 信息: {Err}",
            ConfigType.Name, msgSent, errorCode, errorMsg);
    }

    protected static AxisRef AxisConverter(AkribisAxisId axis) => axis switch
    {
        AkribisAxisId.X => AxisRef.A,
        AkribisAxisId.Y => AxisRef.B,
        AkribisAxisId.Z => AxisRef.C,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };

    private static string MapHomingError(int stat) => stat switch
    {
        -1 => "文件参数错", -2 => "步骤超时", -3 => "中途掉使能",
        -4 => "运动结束原因不符", -5 => "不支持该步骤", -6 => "运动中无法启动",
        -7 => "步骤数越界", -8 => "限位触发异常", -9 => "SetPosition失败",
        -10 => "运动模式错误", -11 => "误差补偿超限", -12 => "换向未完成",
        _ => "未知错误"
    };
}
