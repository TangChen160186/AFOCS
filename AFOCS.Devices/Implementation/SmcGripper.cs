using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation;

/// <summary>
/// SMC 电夹爪配置
/// </summary>
public class SmcGripperConfig
{
    /// <summary>夹爪 ID → 从站地址映射</summary>
    public Dictionary<GripperId, ushort> SlaveAddresses { get; set; } = new()
    {
        [GripperId.LeftCouplingLGripper] = 1012,
        [GripperId.LeftCouplingRGripper] = 1013,
        [GripperId.RightCouplingLGripper] = 1030,
        [GripperId.RightCouplingRGripper] = 1031,
    };
}

/// <summary>
/// SMC 电夹爪接口
/// </summary>
public interface ISmcGripper : IDevice
{
    /// <summary>执行一次定位（启动 → 等待完成 → 复位）</summary>
    Task<Result> Start(GripperId gripperId, int timeoutMs = 5000);

    /// <summary>等待指定状态位变为期望值（轮询 0x6010）</summary>
    /// <param name="gripperId">夹爪 ID</param>
    /// <param name="bit">位号（0-15）</param>
    /// <param name="expectedValue">期望值（true=ON, false=OFF）</param>
    /// <param name="timeoutMs">超时（ms）</param>
    Task<Result> WaitForStatusAsync(GripperId gripperId, ushort bit, bool expectedValue, int timeoutMs = 5000);

    /// <summary>获取夹爪状态字（0x6010）</summary>
    Task<Result<ushort>> GetStatusAsync(GripperId gripperId);

    /// <summary>开启推理推力模式（写 0x7011:00 = 34400）</summary>
    Task<Result> EnablePushForceAsync(GripperId gripperId);

    /// <summary>使能（写 0x7010:00 = 512，bit9=1）</summary>
    Task<Result> EnableAsync(GripperId gripperId);

    /// <summary>检查使能状态（读 0x6010 bit9）</summary>
    Task<Result<bool>> IsEnabledAsync(GripperId gripperId);

    /// <summary>回零操作（检测使能 → 写 4068 → 等 bit10=ON → 恢复 512）</summary>
    Task<Result> HomeAsync(GripperId gripperId, int timeoutMs = 10000);

    /// <summary>报警复位（置位 0x7010 bit11，然后恢复）</summary>
    Task<Result> AlarmResetAsync(GripperId gripperId);

    /// <summary>设置速度（写 0x7021:00）</summary>
    Task<Result> SetSpeedAsync(GripperId gripperId, ushort value);

    /// <summary>设置动作位置（写 0x7022:00）</summary>
    Task<Result> SetPositionAsync(GripperId gripperId, ushort value);

    /// <summary>设置推力上限（写 0x7025:00）</summary>
    Task<Result> SetPushForceUpperAsync(GripperId gripperId, ushort value);

    /// <summary>设置推力下限（写 0x7026:00）</summary>
    Task<Result> SetPushForceLowerAsync(GripperId gripperId, ushort value);

    /// <summary>设置推力距离（写 0x702B:00）</summary>
    Task<Result> SetThrustDistanceAsync(GripperId gripperId, ushort value);

    /// <summary>松开夹爪（推力=0，推力距离=50，然后定位）</summary>
    Task<Result> ReleaseAsync(GripperId gripperId, ushort speed, ushort position, int timeoutMs = 5000);

    /// <summary>闭合夹爪（设置全部5个参数，然后定位）</summary>
    Task<Result> GripAsync(GripperId gripperId, ushort speed, ushort position, ushort forceUpper, 
    ushort forceLower, ushort thrustDistance, int timeoutMs = 5000);
}

/// <summary>
/// SMC 电夹爪设备 —— 通过 EtherCAT PDO 控制
/// </summary>
[Export]
[Export(typeof(ISmcGripper))]
[method: ImportingConstructor]
public class SmcGripper(
    IMotionControlCard motionCard,
    IConfigService configService,
    ILogger logger) : ISmcGripper
{
    private SmcGripperConfig _config = new();

    private const ushort OdStatus = 0x6010;     // TxPDO 状态字
    private const ushort OdControlWord = 0x7010; // RxPDO 控制字（使能等）
    private const ushort OdPushForce = 0x7011;  // RxPDO 推理推力
    private const ushort OdControl = 0x7012;    // RxPDO 控制字（启动/复位）
    private const ushort OdSpeed = 0x7021;       // RxPDO 速度
    private const ushort OdPosition = 0x7022;    // RxPDO 动作位置
    private const ushort OdForceUpper = 0x7025;  // RxPDO 推力上限
    private const ushort OdForceLower = 0x7026;  // RxPDO 推力下限
    private const ushort OdThrustDist = 0x702B;  // RxPDO 推力距离
    private const ushort OdSubIndex = 0x00;

    private const ushort OdBitLen8 = 8;
    private const ushort OdBitLen16 = 16;
    private const ushort OdBitLen32 = 32;

    // 状态位
    private const ushort InpBit = 11;   // INP (定位完成)

    public bool IsConnected => motionCard.IsConnected;

    // ====================================================================
    // IDevice
    // ====================================================================

    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        var config = await configService.LoadAsync<SmcGripperConfig>();
        config ??= new SmcGripperConfig();
        _config = config;
        await configService.SaveAsync(config);

        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接，夹爪无法初始化");

        logger.Information("SMC 电夹爪初始化完成，共 {Count} 个夹爪配置", _config.SlaveAddresses.Count);
        return Result.Success();
    }

    public Task<Result> StopAsync(CancellationToken token = default)
    {
        return Task.FromResult(Result.Success());
    }

    public Task<Result> ReConnectAsync(CancellationToken token = default)
    {
        return InitializeAsync(token);
    }

    // ====================================================================
    // 夹爪操作
    // ====================================================================



    /// <summary>
    /// 执行一次移动加定位：启动 → 等 INP → 延时 → 复位
    /// </summary>
    public async Task<Result> Start(GripperId gripperId, int timeoutMs = 15000)
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        if (!_config.SlaveAddresses.TryGetValue(gripperId, out var slaveAddr))
            return Result.Fail($"夹爪 {gripperId} 未配置从站地址");

        logger.Information("夹爪 {Gripper} (从站{Addr}) 开始启动...", gripperId, slaveAddr);

        try
        {
            // 步骤1: RxPDO 写 0x7012:00 = 1，启动
            var writeResult = await motionCard.WriteRxPDOAsync(slaveAddr, OdControl, OdSubIndex, OdBitLen8, 1);
            if (!writeResult.IsSuccess)
                return Result.Fail($"夹爪 {gripperId} 启动失败: {writeResult.Message}");
            var s = await IsEnabledAsync(gripperId);
            // 步骤2: 等待 INP(bit11) = ON
            var waitResult = await WaitForStatusAsync(gripperId, InpBit, true, timeoutMs);
            if (!waitResult.IsSuccess)
                return Result.Fail($"夹爪 {gripperId} 定位失败: {waitResult.Message}");

            // 步骤3: 延时 50ms
            await Task.Delay(50);

            // 步骤4: RxPDO 写 0x7012:00 = 0，复位
            var resetResult = await motionCard.WriteRxPDOAsync(slaveAddr, OdControl, OdSubIndex, OdBitLen8, 0);
            if (!resetResult.IsSuccess)
                return Result.Fail($"夹爪 {gripperId} 复位失败: {resetResult.Message}");

            logger.Information("夹爪 {Gripper} 定位完成", gripperId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "夹爪 {Gripper} 定位异常", gripperId);
            return Result.Fail($"夹爪定位异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 等待指定状态位变为期望值（轮询 0x6010）
    /// </summary>
    public async Task<Result> WaitForStatusAsync(GripperId gripperId, ushort bit, bool expectedValue, int timeoutMs = 3000)
    {
        if (!_config.SlaveAddresses.TryGetValue(gripperId, out var slaveAddr))
            return Result.Fail($"夹爪 {gripperId} 未配置从站地址");

        int elapsed = 0;
        const int pollInterval = 20;

        while (elapsed < timeoutMs)
        {
            var statusResult = await motionCard.ReadTxPDOAsync(slaveAddr, OdStatus, OdSubIndex, OdBitLen16);
            if (statusResult.IsSuccess)
            {
                ushort status = (ushort)statusResult.Data;
                bool bitValue = (status & (1 << bit)) != 0;

                if (bitValue == expectedValue)
                {
                    logger.Debug("夹爪 {Gripper} bit{bit}={Value}，0x6010=0x{Status:X4}",
                        gripperId, bit, bitValue, status);
                    return Result.Success();
                }
            }

            await Task.Delay(pollInterval);
            elapsed += pollInterval;
        }

        return Result.Fail($"夹爪 {gripperId} bit{bit} 等待 {expectedValue} 超时 ({timeoutMs}ms)");
    }

    /// <summary>
    /// 读取夹爪状态字（TxPDO → 0x6010）
    /// </summary>
    public async Task<Result<ushort>> GetStatusAsync(GripperId gripperId)
    {
        if (!motionCard.IsConnected)
            return Result<ushort>.Fail("运动控制卡未连接");

        if (!_config.SlaveAddresses.TryGetValue(gripperId, out var slaveAddr))
            return Result<ushort>.Fail($"夹爪 {gripperId} 未配置从站地址");

        var result = await motionCard.ReadTxPDOAsync(slaveAddr, OdStatus, OdSubIndex, OdBitLen16);
        if (!result.IsSuccess)
            return Result<ushort>.Fail(result.Message);

        return Result<ushort>.Success((ushort)result.Data);
    }

    /// <summary>
    /// 开启推理推力模式（写 0x7011:00 = 34400）
    /// </summary>
    public async Task<Result> EnablePushForceAsync(GripperId gripperId)
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        if (!_config.SlaveAddresses.TryGetValue(gripperId, out var slaveAddr))
            return Result.Fail($"夹爪 {gripperId} 未配置从站地址");

        var result = await motionCard.WriteRxPDOAsync(slaveAddr, OdPushForce, OdSubIndex, OdBitLen16, 34400);
        if (!result.IsSuccess)
            return Result.Fail($"夹爪 {gripperId} 开启推理推力模式失败: {result.Message}");

        logger.Information("夹爪 {Gripper} 推理推力模式已开启", gripperId);
        return Result.Success();
    }

    /// <summary>
    /// 使能（写 0x7010:00 = 512，bit9=1）
    /// </summary>
    public async Task<Result> EnableAsync(GripperId gripperId)
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        if (!_config.SlaveAddresses.TryGetValue(gripperId, out var slaveAddr))
            return Result.Fail($"夹爪 {gripperId} 未配置从站地址");

        var result = await motionCard.WriteRxPDOAsync(slaveAddr, OdControlWord, OdSubIndex, OdBitLen16, 512);
        if (!result.IsSuccess)
            return Result.Fail($"夹爪 {gripperId} 使能失败: {result.Message}");

        logger.Information("夹爪 {Gripper} 使能完成", gripperId);
        return Result.Success();
    }

    /// <summary>
    /// 检查使能状态（读 0x6010 bit9）
    /// </summary>
    public async Task<Result<bool>> IsEnabledAsync(GripperId gripperId)
    {
        var statusResult = await GetStatusAsync(gripperId);
        if (!statusResult.IsSuccess)
            return Result<bool>.Fail(statusResult.Message);

        var enabled = (statusResult.Data & (1 << 9)) != 0;
        return Result<bool>.Success(enabled);
    }

    /// <summary>
    /// 回零：检测使能 → 写 0x7010 = 4068 → 等 bit10=ON → 恢复 512
    /// </summary>
    public async Task<Result> HomeAsync(GripperId gripperId, int timeoutMs = 10000)
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        if (!_config.SlaveAddresses.TryGetValue(gripperId, out var slaveAddr))
            return Result.Fail($"夹爪 {gripperId} 未配置从站地址");

        // 步骤1: 检查使能
        var enabledResult = await IsEnabledAsync(gripperId);
        if (!enabledResult.IsSuccess)
            return Result.Fail($"夹爪 {gripperId} 读取使能状态失败: {enabledResult.Message}");
        if (!enabledResult.Data)
            return Result.Fail($"夹爪 {gripperId} 未使能，请先调用 EnableAsync");

        logger.Information("夹爪 {Gripper} 开始回零...", gripperId);

        try
        {
            // 步骤2: 写 0x7010 = 4068
            var writeResult = await motionCard.WriteRxPDOAsync(slaveAddr, OdControlWord, OdSubIndex, OdBitLen16, 4608);
            if (!writeResult.IsSuccess)
                return Result.Fail($"夹爪 {gripperId} 回零启动失败: {writeResult.Message}");

            // 步骤3: 等待 bit10 = ON
            var waitResult = await WaitForStatusAsync(gripperId, 10, true, timeoutMs);
            if (!waitResult.IsSuccess)
                return Result.Fail($"夹爪 {gripperId} 回零失败: {waitResult.Message}");

            // 步骤4: 恢复 512（关闭回零，保持使能）
            var resetResult = await motionCard.WriteRxPDOAsync(slaveAddr, OdControlWord, OdSubIndex, OdBitLen16, 512);
            if (!resetResult.IsSuccess)
                return Result.Fail($"夹爪 {gripperId} 回零关闭失败: {resetResult.Message}");

            logger.Information("夹爪 {Gripper} 回零完成", gripperId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "夹爪 {Gripper} 回零异常", gripperId);
            return Result.Fail($"夹爪回零异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 报警复位：置位 bit11 → 恢复 512
    /// </summary>
    public async Task<Result> AlarmResetAsync(GripperId gripperId)
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        if (!_config.SlaveAddresses.TryGetValue(gripperId, out var slaveAddr))
            return Result.Fail($"夹爪 {gripperId} 未配置从站地址");

        try
        {
            // 置位 bit11: 512 | 2048 = 2560
            var setResult = await motionCard.WriteRxPDOAsync(slaveAddr, OdControlWord, OdSubIndex, OdBitLen16, 2560);
            if (!setResult.IsSuccess)
                return Result.Fail($"夹爪 {gripperId} 报警复位失败: {setResult.Message}");

            await Task.Delay(50);

            // 恢复 512
            var resetResult = await motionCard.WriteRxPDOAsync(slaveAddr, OdControlWord, OdSubIndex, OdBitLen16, 512);
            if (!resetResult.IsSuccess)
                return Result.Fail($"夹爪 {gripperId} 报警复位恢复失败: {resetResult.Message}");

            logger.Information("夹爪 {Gripper} 报警复位完成", gripperId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "夹爪 {Gripper} 报警复位异常", gripperId);
            return Result.Fail($"夹爪报警复位异常: {ex.Message}");
        }
    }

    /// <summary>设置速度（0x7021）</summary>
    public async Task<Result> SetSpeedAsync(GripperId gripperId, ushort value)
        => await SetParameterAsync(gripperId, OdSpeed, value, "速度");

    /// <summary>设置动作位置（0x7022）</summary>
    public async Task<Result> SetPositionAsync(GripperId gripperId, ushort value)
        => await SetParameterAsync(gripperId, OdPosition, value, "动作位置",OdBitLen32);

    /// <summary>设置推力上限（0x7025）</summary>
    public async Task<Result> SetPushForceUpperAsync(GripperId gripperId, ushort value)
        => await SetParameterAsync(gripperId, OdForceUpper, value, "推力上限");

    /// <summary>设置推力下限（0x7026）</summary>
    public async Task<Result> SetPushForceLowerAsync(GripperId gripperId, ushort value)
        => await SetParameterAsync(gripperId, OdForceLower, value, "推力下限");

    /// <summary>设置推力距离（0x702B）</summary>
    public async Task<Result> SetThrustDistanceAsync(GripperId gripperId, ushort value)
        => await SetParameterAsync(gripperId, OdThrustDist, value, "推力距离",OdBitLen32);

    private async Task<Result> SetParameterAsync(GripperId gripperId, ushort odIndex, ushort value, string paramName,ushort dataLength = OdBitLen16)
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        if (!_config.SlaveAddresses.TryGetValue(gripperId, out var slaveAddr))
            return Result.Fail($"夹爪 {gripperId} 未配置从站地址");

        var result = await motionCard.WriteRxPDOAsync(slaveAddr, odIndex, OdSubIndex, dataLength, value);
        if (!result.IsSuccess)
            return Result.Fail($"夹爪 {gripperId} 设置{paramName}={value}失败: {result.Message}");

        logger.Debug("夹爪 {Gripper} 设置{Param}={Value}", gripperId, paramName, value);
        return Result.Success();
    }

    /// <summary>
    /// 松开夹爪：设置速度/位置，推力清零，推力距离=50，执行定位
    /// </summary>
    public async Task<Result> ReleaseAsync(GripperId gripperId, ushort speed, ushort position, int timeoutMs = 5000)
    {
        var setSpeed = await SetSpeedAsync(gripperId, speed);
        if (!setSpeed.IsSuccess) return setSpeed;

        var setPos = await SetPositionAsync(gripperId, position);
        if (!setPos.IsSuccess) return setPos;

        var setForceUp = await SetPushForceUpperAsync(gripperId, 0);
        if (!setForceUp.IsSuccess) return setForceUp;

        var setForceLow = await SetPushForceLowerAsync(gripperId, 0);
        if (!setForceLow.IsSuccess) return setForceLow;

        var setDist = await SetThrustDistanceAsync(gripperId, 50);
        if (!setDist.IsSuccess) return setDist;

        return await Start(gripperId, timeoutMs);
    }

    /// <summary>
    /// 闭合夹爪：设置全部5个参数，执行定位
    /// </summary>
    public async Task<Result> GripAsync(GripperId gripperId, ushort speed, ushort position, ushort forceUpper, ushort forceLower, ushort thrustDistance, int timeoutMs = 5000)
    {
        var setSpeed = await SetSpeedAsync(gripperId, speed);
        if (!setSpeed.IsSuccess) return setSpeed;

        var setPos = await SetPositionAsync(gripperId, position);
        if (!setPos.IsSuccess) return setPos;

        var setForceUp = await SetPushForceUpperAsync(gripperId, forceUpper);
        if (!setForceUp.IsSuccess) return setForceUp;

        var setForceLow = await SetPushForceLowerAsync(gripperId, forceLower);
        if (!setForceLow.IsSuccess) return setForceLow;

        var setDist = await SetThrustDistanceAsync(gripperId, thrustDistance);
        if (!setDist.IsSuccess) return setDist;

        return await Start(gripperId, timeoutMs);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
