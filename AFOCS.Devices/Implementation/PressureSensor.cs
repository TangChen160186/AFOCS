using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation;

/// <summary>
/// 压力传感器配置
/// </summary>
public class PressureSensorConfig
{
    /// <summary>压力传感器 ID → 从站地址映射</summary>
    public Dictionary<PressureSensorId, ushort> SlaveAddresses { get; set; } = new()
    {
        [PressureSensorId.LeftCouplingLPressure] = 1014,
        [PressureSensorId.LeftCouplingRPressure] = 1015,
        [PressureSensorId.LeftDispensePressure] = 1016,
        [PressureSensorId.RightCouplingLPressure] = 1017,
        [PressureSensorId.RightCouplingRPressure] = 1018,
        [PressureSensorId.RightDispensePressure] = 1019,
    };
}

/// <summary>
/// 压力传感器设备 —— 通过 EtherCAT PDO 控制
/// </summary>
[Export]
[Export(typeof(IPressureSensor))]
[method: ImportingConstructor]
public class PressureSensor(
    IMotionControlCard motionCard,
    IConfigService configService,
    ILogger logger) : IPressureSensor
{
    private PressureSensorConfig _config = new();

    // OD 地址常量
    private const ushort OdReadPressure = 0x6000;   // 读取压力值，子索引 1=X, 2=Y, 3=Z
    private const ushort OdZeroControl = 0x7000;     // 清零控制，子索引 0x00
    private const ushort OdSubIndex = 0x00;
    private const ushort OdBitLen32 = 32;

    public bool IsConnected => motionCard.IsConnected;

    // ====================================================================
    // IDevice
    // ====================================================================

    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        var config = await configService.LoadAsync<PressureSensorConfig>();
        config ??= new PressureSensorConfig();
        _config = config;
        await configService.SaveAsync(config);

        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接，压力传感器无法初始化");

        logger.Information("压力传感器初始化完成，共 {Count} 个传感器配置", _config.SlaveAddresses.Count);
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
    // 压力传感器操作
    // ====================================================================

    /// <summary>
    /// 根据通道获取对应的子索引（X→1, Y→2, Z→3）
    /// </summary>
    private static ushort ChannelToSubIndex(PressureChannel channel) => channel switch
    {
        PressureChannel.X => 1,
        PressureChannel.Y => 2,
        PressureChannel.Z => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(channel)),
    };

    /// <summary>
    /// 根据通道获取对应的清零指令（X→0x5AA501, Y→0x5AA502, Z→0x5AA503）
    /// </summary>
    private static int ChannelToZeroValue(PressureChannel channel) => channel switch
    {
        PressureChannel.X => 0x5AA501,
        PressureChannel.Y => 0x5AA502,
        PressureChannel.Z => 0x5AA503,
        _ => throw new ArgumentOutOfRangeException(nameof(channel)),
    };

    /// <summary>
    /// 读取指定传感器指定通道的压力值（0x6000:subIndex）
    /// </summary>
    public async Task<Result<int>> ReadAsync(PressureSensorId sensorId, PressureChannel channel)
    {
        if (!motionCard.IsConnected)
            return Result<int>.Fail("运动控制卡未连接");

        if (!_config.SlaveAddresses.TryGetValue(sensorId, out var slaveAddr) || slaveAddr == 0)
            return Result<int>.Fail($"压力传感器 {sensorId} 未配置从站地址");

        var subIndex = ChannelToSubIndex(channel);

        var result = await motionCard.ReadTxPDOAsync(slaveAddr, OdReadPressure, subIndex, OdBitLen32);
        if (!result.IsSuccess)
            return Result<int>.Fail($"压力传感器 {sensorId} 通道{channel} 读取失败: {result.Message}");

        logger.Debug("压力传感器 {Sensor} 通道{Channel} = {Value}", sensorId, channel, result.Data);
        return Result<int>.Success(result.Data);
    }

    /// <summary>
    /// 读取指定传感器全部三个通道的压力值（X/Y/Z）
    /// </summary>
    public async Task<Result<(int X, int Y, int Z)>> ReadAllAsync(PressureSensorId sensorId)
    {
        var xResult = await ReadAsync(sensorId, PressureChannel.X);
        if (!xResult.IsSuccess)
            return Result<(int, int, int)>.Fail(xResult.Message);

        var yResult = await ReadAsync(sensorId, PressureChannel.Y);
        if (!yResult.IsSuccess)
            return Result<(int, int, int)>.Fail(yResult.Message);

        var zResult = await ReadAsync(sensorId, PressureChannel.Z);
        if (!zResult.IsSuccess)
            return Result<(int, int, int)>.Fail(zResult.Message);

        return Result<(int, int, int)>.Success((xResult.Data, yResult.Data, zResult.Data));
    }

    /// <summary>
    /// 对指定传感器指定通道进行清零（0x7000:00 写入对应清零指令）
    /// </summary>
    public async Task<Result> ZeroAsync(PressureSensorId sensorId, PressureChannel channel)
    {
        if (!motionCard.IsConnected)
            return Result.Fail("运动控制卡未连接");

        if (!_config.SlaveAddresses.TryGetValue(sensorId, out var slaveAddr) || slaveAddr == 0)
            return Result.Fail($"压力传感器 {sensorId} 未配置从站地址");

        var zeroValue = ChannelToZeroValue(channel);

        logger.Information("压力传感器 {Sensor} 通道{Channel} 开始清零（写入 0x{ZeroValue:X}）...", sensorId, channel, zeroValue);

        var result = await motionCard.WriteRxPDOAsync(slaveAddr, OdZeroControl, OdSubIndex, OdBitLen32, zeroValue);
        if (!result.IsSuccess)
            return Result.Fail($"压力传感器 {sensorId} 通道{channel} 清零失败: {result.Message}");

        logger.Information("压力传感器 {Sensor} 通道{Channel} 清零完成", sensorId, channel);
        return Result.Success();
    }

    /// <summary>
    /// 对指定传感器全部三个通道进行清零
    /// </summary>
    public async Task<Result> ZeroAllAsync(PressureSensorId sensorId)
    {
        var xResult = await ZeroAsync(sensorId, PressureChannel.X);
        if (!xResult.IsSuccess) return xResult;

        var yResult = await ZeroAsync(sensorId, PressureChannel.Y);
        if (!yResult.IsSuccess) return yResult;

        var zResult = await ZeroAsync(sensorId, PressureChannel.Z);
        if (!zResult.IsSuccess) return zResult;

        logger.Information("压力传感器 {Sensor} 全部通道清零完成", sensorId);
        return Result.Success();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
