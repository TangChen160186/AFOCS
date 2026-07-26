namespace AFOCS.Infrastructure
{
    public enum WorkPos
    {
        Left,
        Right,
        Common,// 代表通用的
    }

    /// <summary>
    /// 压力传感器枚举定义（EtherCAT 总线控制）
    /// </summary>
    public enum PressureSensorId
    {
        /// <summary>左工位左耦合压力传感器</summary>
        LeftCouplingLPressure = 0,
        /// <summary>左工位右耦合压力传感器</summary>
        LeftCouplingRPressure = 1,
        /// <summary>左工位点胶压力传感器</summary>
        LeftDispensePressure = 2,

        /// <summary>右工位左耦合压力传感器</summary>
        RightCouplingLPressure = 3,
        /// <summary>右工位右耦合压力传感器</summary>
        RightCouplingRPressure = 4,
        /// <summary>右工位点胶压力传感器</summary>
        RightDispensePressure = 5,
    }

    /// <summary>
    /// 压力传感器通道（X/Y/Z）
    /// </summary>
    public enum PressureChannel
    {
        /// <summary>X 通道（子索引 1）</summary>
        X = 0,
        /// <summary>Y 通道（子索引 2）</summary>
        Y = 1,
        /// <summary>Z 通道（子索引 3）</summary>
        Z = 2,
    }
}
