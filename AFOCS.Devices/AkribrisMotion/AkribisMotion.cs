using AAMotion;
using AFOCS.Infrastructure;
using Serilog;
using System.Diagnostics;

namespace AFOCS.Devices.AkribrisMotion;

public abstract class AkribisMotion<TConfig> : IAkribisMotion where TConfig: AkribisCouplingConfig
{
    private readonly ILogger _logger;
    private readonly IConfigService _configService;
    private readonly MotionController _controller;

    private AkribisCouplingConfig _config = new();
    public bool IsConnected => _controller.IsConnected;
    public abstract WorkPos WorkPos { get; }
    public abstract AkribisMotionType AkribisMotionType { get; }
    /// <summary>
    /// Checks if AACommServer is running; if not, launches it from the PCSuite install path.
    /// </summary>
    static void EnsureAACommServerRunning()
    {
        const string serverProcessName = "AACommServer";
        var existing = Process.GetProcessesByName(serverProcessName);
        if (existing.Length > 0)
        {
            Console.WriteLine($"[AACommServer] Already running (PID {existing[0].Id})");
            return;
        }

        Console.WriteLine("[AACommServer] Not running, launching...");
        const string pcsSuitePath = @"C:\Program Files (x86)\Agito\PCSuite\AACommServer.exe";
        if (!File.Exists(pcsSuitePath))
        {
            Console.WriteLine($"[AACommServer] Not found at {pcsSuitePath}");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = pcsSuitePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(startInfo);
            Thread.Sleep(1500); // give the server time to start
            Console.WriteLine("[AACommServer] Launched successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AACommServer] Failed to launch: {ex.Message}");
        }
    }

    protected AkribisMotion(IConfigService configService, ILogger logger)
    {
        EnsureAACommServerRunning();
        _configService = configService;
        _logger = logger;
        _controller = AAMotionAPI.Initialize(ControllerType.AGD301);
    }


    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        var loaded = await _configService.LoadAsync(typeof(TConfig));
        if (loaded is AkribisCouplingConfig config)
        {
            _config = config;
        }
        else
        {
            _config = (AkribisCouplingConfig)Activator.CreateInstance(typeof(TConfig))!;
            await _configService.SaveAsync(typeof(TConfig), _config); ;
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
            return Result.Fail(ResultCode.Fail, $"使能或者换向失败,A:{readyA},B:{readyB},C:{readyC}");

        _logger.Information("[{Type}] 初始化成功, IP={Ip}", GetType().Name, _config.Ip);

        StartMonitoring();
        return Result.Success();
    }

    #region 使能

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


    #endregion

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
                return Result.Success();
            }
            if (timeoutMs > 0 && elapsed >= timeoutMs)
            {
                await StopAxisAsync(axis);
                var homeFailReason = MapHomingError(homingStat);
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
        var s = speed ?? p.Speed ;
        var a = accel ?? p.Accel ;
        var d = decel ?? p.Decel;
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
        var s = speed ?? p.Speed;
        var a = accel ?? p.Accel;
        var d = decel ?? p.Decel;
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
        var s = speed ?? p.Speed;
        var a = accel ?? p.Accel ;
        var d = decel ?? p.Decel ;

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
        _logger.Information("[{Type}] 后台轮询已启动，间隔 {Interval}ms", GetType().Name, intervalMs);
        _ = Task.Run(() => PollLoopAsync(intervalMs, _pollCts.Token), _pollCts.Token);
    }

    private void StopMonitoring()
    {
        if (!IsMonitoring) return;
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
        IsMonitoring = false;
        _logger.Information("[{Type}] 后台轮询已停止", GetType().Name);
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

                int newX = (int)axisA.Pos;
                int newY = (int)axisB.Pos;
                int newZ = (int)axisC.Pos;

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
                _logger.Error("[{Type}] 轮询异常: {Msg}", GetType().Name, ex.Message);
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

    public async Task<Result> ReConnectAsync(CancellationToken token = default)
    {
        if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
        var reConnectTask = Task.Run(() => _controller.TryReconnect(_config.Ip, _config.Ark));
        var success = await reConnectTask.ConfigureAwait(false);
        if(!success)
            return Result.Fail(ResultCode.Fail, "重连设备失败");
        return Result.Success();
    }


    #region 配置
    public AkribisCouplingConfig GetConfig() => _config.Clone();

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
        await _configService.SaveAsync(typeof(TConfig), _config);
    }


    #endregion


    #region 耦合找光

    /// <summary>
    /// 单轴耦合（单轴找光）：沿指定轴扫描，返回各通道光功率数据与角度。
    /// </summary>
    public async Task<Result<AkribisCouplingResult>> SingleAxisCouplingAsync(SingleAxisCouplingArgs args, CancellationToken token = default)
    {
        if (!IsConnected) return Result<AkribisCouplingResult>.Fail(ResultCode.Fail, "未连接设备");

        try
        {
            // 运动轴: AGenData[510] (0:A轴, 1:B轴, 2:C轴)
            SetAGenData(510, args.Axis);
            // 采样次数
            SetAGenData(512, 2);
            // 平坦区衰减梯度
            SetAGenData(509, 0);
            // 最优系数
            SetAGenData(508, 0);
            // 采样间距
            SetAGenData(513, (int)args.SamplingInterval);
            // 起始距离(相对当前位置)
            SetAGenData(514, (int)args.StartDistance);
            // 停止距离(相对当前位置)
            SetAGenData(515, (int)args.StopDistance);
            // 最大扫描速度
            SetAGenData(517, (int)args.MaxScanSpeed);
            // 最大回归速度
            SetAGenData(518, (int)args.MaxReturnSpeed);
            // 间距宽度(mm转脉冲): 20微米=4096脉冲
            int spacingPulse = (int)(args.SpacingWidth * 1000.0 / 20.0 * 4096.0);
            SetAGenData(506, spacingPulse);
            // 采集通道
            SetAGenData(511, args.AcquireChannel);
            // 转角系数
            SetAGenData(528, 0);
            SetAGenData(531, 0);

            // 启动单轴找光: AGenData[500]=5
            SetAGenData(500, 5);

            await WaitCouplingDoneAsync(token);

            var result = new AkribisCouplingResult
            {
                ChannelPower = GetSingleAxisCouplingPowerData(),
                Angle = ParseAGenDataDouble(817) / 1000.0,
                SuccessCode = ParseAGenDataInt(650),
            };

            _logger.Information("[{Type}] 单轴耦合完成: 角度={Angle:F4}°, 成功码={Code}", GetType().Name, result.Angle, result.SuccessCode);
            return Result<AkribisCouplingResult>.Success(result);
        }
        catch (OperationCanceledException)
        {
            StopCoupling();
            return Result<AkribisCouplingResult>.Fail(ResultCode.Fail, "单轴耦合已取消");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[{Type}] 单轴耦合异常", GetType().Name);
            return Result<AkribisCouplingResult>.Fail(ResultCode.Fail, $"单轴耦合异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 螺旋耦合（螺旋找光）：双轴螺旋扫描，返回各通道光功率数据。
    /// </summary>
    public async Task<Result<AkribisCouplingResult>> SpiralCouplingAsync(SpiralCouplingArgs args, CancellationToken token = default)
    {
        if (!IsConnected) return Result<AkribisCouplingResult>.Fail(ResultCode.Fail, "未连接设备");

        try
        {
            // 1#运动轴
            SetAGenData(521, args.Axis1);
            // 2#运动轴
            SetAGenData(522, args.Axis2);
            // 螺距
            SetAGenData(526, (int)args.Pitch);
            // 最大扫描半径
            SetAGenData(534, (int)args.MaxScanRadius);
            // 最大扫描速度
            SetAGenData(529, (int)args.MaxScanSpeed);
            // 最大回归速度
            SetAGenData(530, (int)args.MaxReturnSpeed);
            // 采集通道
            SetAGenData(511, args.AcquireChannel);

            // 启动螺旋找光: AGenData[500]=1
            SetAGenData(500, 1);

            // 轮询状态，最多 3000 次(300s)
            bool done = false;
            for (int i = 0; i < 3000; i++)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(100, token);
                if (int.TryParse(GetAGenData(500), out int status) && status == 0)
                {
                    done = true;
                    break;
                }
            }

            if (!done)
            {
                StopCoupling();
                return Result<AkribisCouplingResult>.Fail(ResultCode.Fail, "螺旋耦合超时");
            }

            var result = new AkribisCouplingResult
            {
                ChannelPower = GetSpiralCouplingPowerData(),
                ErrorCode = ParseAGenDataInt(602),
                SuccessCode = ParseAGenDataInt(650),
            };

            _logger.Information("[{Type}] 螺旋耦合完成: 报错码={Error}, 成功码={Code}", GetType().Name, result.ErrorCode, result.SuccessCode);
            return Result<AkribisCouplingResult>.Success(result);
        }
        catch (OperationCanceledException)
        {
            StopCoupling();
            return Result<AkribisCouplingResult>.Fail(ResultCode.Fail, "螺旋耦合已取消");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[{Type}] 螺旋耦合异常", GetType().Name);
            return Result<AkribisCouplingResult>.Fail(ResultCode.Fail, $"螺旋耦合异常: {ex.Message}");
        }
    }

    // ========== AGenData 辅助 ==========

    private void SetAGenData(int address, int value) => SendRawCommand($"AGenData[{address}]={value}");

    private string GetAGenData(int address) => SendRawCommand($"AGenData[{address}]");

    private int ParseAGenDataInt(int address) => int.TryParse(GetAGenData(address), out int v) ? v : 0;

    private double ParseAGenDataDouble(int address) => double.TryParse(GetAGenData(address), out double v) ? v : 0;

    private string SendRawCommand(string command)
    {
        string response = "";
        _controller.SendCommandString(command, out response);
        return response?.Trim() ?? "";
    }

    private async Task WaitCouplingDoneAsync(CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(100, token);
            if (int.TryParse(GetAGenData(500), out int status) && status == 0)
                return;
        }
    }

    private void StopCoupling()
    {
        try { SendRawCommand("AGenData[500]=0"); } catch { /* 忽略停止异常 */ }
    }

    // ========== 数据采集 ==========

    /// <summary>从控制器获取 AGenData 全部数据（16000 个值，逗号分隔）</summary>
    private double[]? FetchAGenData()
    {
        try
        {
            string response = SendRawCommand("AGenDataUpload");
            if (string.IsNullOrWhiteSpace(response)) return null;

            var parts = response.Split(',');
            var data = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                double.TryParse(parts[i].Trim(), out data[i]);
            return data;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[{Type}] 获取 AGenData 数据失败", GetType().Name);
            return null;
        }
    }

    /// <summary>单轴找光功率数据：CH1=[4000-6999], CH2=[7000-9999], CH3=[10000-12999], CH4=[13000-15999]</summary>
    private Dictionary<int, List<double>>? GetSingleAxisCouplingPowerData()
    {
        var data = FetchAGenData();
        if (data == null || data.Length < 16000) return null;

        int validLength = (int)data[735];
        if (validLength <= 0) validLength = 3000;
        if (validLength > 3000) validLength = 3000;

        return new Dictionary<int, List<double>>
        {
            { 1, data.Skip(4000).Take(validLength).ToList() },
            { 2, data.Skip(7000).Take(validLength).ToList() },
            { 3, data.Skip(10000).Take(validLength).ToList() },
            { 4, data.Skip(13000).Take(validLength).ToList() },
        };
    }

    /// <summary>螺旋找光功率数据：CH1=[6000-8499], CH2=[8500-10999], CH3=[11000-13499], CH4=[13500-15999]</summary>
    private Dictionary<int, List<double>>? GetSpiralCouplingPowerData()
    {
        var data = FetchAGenData();
        if (data == null || data.Length < 16000) return null;

        int validLength = (int)data[735];
        if (validLength <= 0) validLength = 2500;
        if (validLength > 2500) validLength = 2500;

        return new Dictionary<int, List<double>>
        {
            { 1, data.Skip(6000).Take(validLength).ToList() },
            { 2, data.Skip(8500).Take(validLength).ToList() },
            { 3, data.Skip(11000).Take(validLength).ToList() },
            { 4, data.Skip(13500).Take(validLength).ToList() },
        };
    }

    #endregion


    public void Dispose()
    {
        StopMonitoring();
        _controller.Dispose();
    }



    // ========== 内部辅助 ==========

    private async Task<Result> WaitForMotionDone(AxisRef ar, AkribisAxisId axis, int timeoutMs)
    {
        const int interval = 20;
        // 再等待运动完成
        int elapsed = 0;
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
        if (!_controller.GetAxis(axis).IsCommutated())
        {
            _logger.Information("[{Type}] 换向未完成，正在执行 AutoPhase...", GetType().Name);
            AAMotionAPI.AutoPhase(_controller, axis, 5000);
            Thread.Sleep(100);
            if (!_controller.GetAxis(axis).IsCommutated()) return false;
        }
        if (_controller.GetAxis(axis).MotorOn == 0)
        {
            _logger.Information("[{Type}] 电机未使能，正在使能...", GetType().Name);
            AAMotionAPI.MotorOn(_controller, axis);
            Thread.Sleep(100);
            return _controller.GetAxis(axis).MotorOn == 1;
        }
        return true;
    }

    private void OnControllerErrorOccurred(int errorCode, string msgSent, string errorMsg)
    {
        _logger.Error("[{Type}] 控制器错误 - 发送: {Msg}, 错误码: {Code}, 信息: {Err}",
            GetType().Name, msgSent, errorCode, errorMsg);
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
