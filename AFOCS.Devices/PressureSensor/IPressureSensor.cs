using AFOCS.Infrastructure;

namespace AFOCS.Devices.PressureSensor;

public interface IPressureSensor : IDevice
{
    PressureSensorType SensorType { get; }

    event EventHandler<PressureDataChangedEventArgs>? DataChanged;
    event EventHandler<PressureAlarmEventArgs>? AlarmTriggered;
    Task StartMonitoring(int intervalMs = 100);
    void StopMonitoring();

    bool IsMonitoring { get; }

    int GetX();

    int GetY();

    int GetZ();

    Task<Result<int>> ReadXAsync();

    Task<Result<int>> ReadYAsync();

    Task<Result<int>> ReadZAsync();
    Task<Result<(int X, int Y, int Z)>> ReadAllAsync();

    Task<Result> ZeroAllAsync();

    PressureSensorConfig GetConfig();
    Task SaveConfigAsync(PressureSensorConfig config);
}
