namespace AFOCS.Devices.PressureSensor;

public class PressureAlarmEventArgs(PressureChannel channel, int currentValue, int threshold, bool isActive)
    : EventArgs
{
    public PressureChannel Channel { get; } = channel;
    public int CurrentValue { get; } = currentValue;
    public int Threshold { get; } = threshold;
    public bool IsActive { get; } = isActive;
    public DateTime Timestamp { get; } = DateTime.Now;
}