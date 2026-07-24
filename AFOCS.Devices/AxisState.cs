namespace AFOCS.Devices
{
    /// <summary>轴类型</summary>
    public enum AxisKind { BusAxis, LinearAxis, Gripper }

    /// <summary>轴状态变化事件参数</summary>
    public class AxisStateChangedEventArgs : EventArgs
    {
        public AxisKind Kind { get; }
        public int AxisId { get; }
        public string Name { get; }
        public double Position { get; }
        public double Speed { get; }
        public bool IsEnabled { get; }
        public bool IsMoving { get; }
        public DateTime Timestamp { get; }

        public AxisStateChangedEventArgs(AxisKind kind, int axisId, string name,
            double position, double speed, bool isEnabled, bool isMoving)
        {
            Kind = kind;
            AxisId = axisId;
            Name = name;
            Position = position;
            Speed = speed;
            IsEnabled = isEnabled;
            IsMoving = isMoving;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>轴状态快照（用于批量查询）</summary>
    public class AxisSnapshot
    {
        public AxisKind Kind { get; init; }
        public int AxisId { get; init; }
        public string Name { get; init; } = "";
        public double Position { get; set; }
        public double Speed { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsMoving { get; set; }
    }
}
