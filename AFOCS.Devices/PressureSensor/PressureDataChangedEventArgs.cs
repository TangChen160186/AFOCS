namespace AFOCS.Devices.PressureSensor;

public class PressureDataChangedEventArgs(int x, int y, int z) : EventArgs
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;
    public DateTime Timestamp { get; } = DateTime.Now;
}