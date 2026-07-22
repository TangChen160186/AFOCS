using AFOCS.Infrastructure;

namespace AFOCS.Devices;

/// <summary>
/// 压力传感器接口（EtherCAT 总线控制，每传感器含 X/Y/Z 三通道）
/// </summary>
public interface IPressureSensor : IDevice
{
    /// <summary>读取指定传感器指定通道的压力值（0x6000:subIndex）</summary>
    Task<Result<int>> ReadAsync(PressureSensorId sensorId, PressureChannel channel);

    /// <summary>读取指定传感器全部三个通道的压力值（X/Y/Z）</summary>
    Task<Result<(int X, int Y, int Z)>> ReadAllAsync(PressureSensorId sensorId);

    /// <summary>对指定传感器指定通道进行清零（0x7000:00 写入对应清零指令）</summary>
    Task<Result> ZeroAsync(PressureSensorId sensorId, PressureChannel channel);

    /// <summary>对指定传感器全部三个通道进行清零</summary>
    Task<Result> ZeroAllAsync(PressureSensorId sensorId);
}
