using System.ComponentModel;

namespace AFOCS.Devices
{
    /// <summary>
    /// 轴运动参数（速度曲线）
    /// </summary>
    public class AxisMotionParams : ICloneable
    {
        /// <summary>脉冲当量（pulse/unit），如 1000 表示 1000 脉冲 = 1mm</summary>
        [Description("脉冲当量 (pulse/unit)")]
        public double Equiv { get; set; } = 1000;

        /// <summary>起始速度（unit/s）</summary>
        [Description("起始速度 (unit/s)")]
        public double MinVel { get; set; } = 10;

        /// <summary>最大速度（unit/s）</summary>
        [Description("最大速度 (unit/s)")]
        public double MaxVel { get; set; } = 100;

        /// <summary>加速时间（s）</summary>
        [Description("加速时间 (s)")]
        public double Tacc { get; set; } = 0.1;

        /// <summary>减速时间（s）</summary>
        [Description("减速时间 (s)")]
        public double Tdec { get; set; } = 0.1;

        /// <summary>停止速度（unit/s）</summary>
        [Description("停止速度 (unit/s)")]
        public double StopVel { get; set; } = 10;

        /// <summary>S段曲线时间（s），0 表示梯形曲线</summary>
        [Description("S段时间 (s)")]
        public double SPara { get; set; } = 0;

        public AxisMotionParams Clone() => new()
        {
            Equiv = Equiv, MinVel = MinVel, MaxVel = MaxVel,
            Tacc = Tacc, Tdec = Tdec, StopVel = StopVel, SPara = SPara,
        };

        object ICloneable.Clone() => Clone();
    }

    /// <summary>
    /// 轴回零参数
    /// </summary>
    public class AxisHomeParams : ICloneable
    {
        /// <summary>
        /// 回零模式：
        /// 1=找负限位反找Z相, 2=找正限位反找Z相,
        /// 17=找负限位, 18=找正限位,
        /// 33=正向找Z相, 34=负向找Z相, 35=当前位置设为原点
        /// </summary>
        [Description("回零模式")]
        public ushort HomeMode { get; set; } = 33;

        /// <summary>回零低速（unit/s），精找原点</summary>
        [Description("回零低速 (unit/s)")]
        public double LowVel { get; set; } = 10;

        /// <summary>回零高速（unit/s），快速接近原点</summary>
        [Description("回零高速 (unit/s)")]
        public double HighVel { get; set; } = 100;

        /// <summary>回零加速时间（s）</summary>
        [Description("回零加速时间 (s)")]
        public double Tacc { get; set; } = 0.1;

        /// <summary>回零减速时间（s）</summary>
        [Description("回零减速时间 (s)")]
        public double Tdec { get; set; } = 0.1;

        /// <summary>回零偏移量（unit），回零完成后偏移的距离</summary>
        [Description("回零偏移量 (unit)")]
        public double OffsetPos { get; set; } = 0;

        public AxisHomeParams Clone() => new()
        {
            HomeMode = HomeMode, LowVel = LowVel, HighVel = HighVel,
            Tacc = Tacc, Tdec = Tdec, OffsetPos = OffsetPos,
        };

        object ICloneable.Clone() => Clone();
    }

    /// <summary>
    /// 单个轴的完整配置
    /// </summary>
    public class AxisConfig : ICloneable
    {
        /// <summary>轴标识</summary>
        public AxisId AxisId { get; set; }

        /// <summary>运动参数</summary>
        public AxisMotionParams Motion { get; set; } = new();

        /// <summary>回零参数</summary>
        public AxisHomeParams Home { get; set; } = new();

        /// <summary>负方向软限位（unit）</summary>
        [Description("负软限位 (unit)")]
        public double NegativeSoftLimit { get; set; } = -100000;

        /// <summary>正方向软限位（unit）</summary>
        [Description("正软限位 (unit)")]
        public double PositiveSoftLimit { get; set; } = 100000;

        /// <summary>是否启用软限位</summary>
        [Description("启用软限位")]
        public bool SoftLimitEnabled { get; set; } = false;

        /// <summary>最高速度上限（unit/s），用于界面校验</summary>
        [Description("最高速度 (unit/s)")]
        public double MaxSpeed { get; set; } = 200;

        /// <summary>每圈脉冲数</summary>
        [Description("每圈脉冲数")]
        public int PulsePerRev { get; set; } = 10000;

        public AxisConfig Clone() => new()
        {
            AxisId = AxisId,
            Motion = Motion.Clone(),
            Home = Home.Clone(),
            NegativeSoftLimit = NegativeSoftLimit,
            PositiveSoftLimit = PositiveSoftLimit,
            SoftLimitEnabled = SoftLimitEnabled,
            MaxSpeed = MaxSpeed,
            PulsePerRev = PulsePerRev,
        };

        object ICloneable.Clone() => Clone();
    }

    /// <summary>
    /// 所有轴配置集合（用于持久化）
    /// </summary>
    public class AxisConfigCollection
    {
        public Dictionary<int, AxisConfig> Axes { get; set; } = [];
    }
}
