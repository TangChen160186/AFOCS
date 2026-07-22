namespace AFOCS.Infrastructure
{
    public enum WorkPos
    {
        Left,
        Right,
        Common,// 代表通用的
    }

    /// <summary>
    /// 轴控制类型
    /// </summary>
    public enum AxisControlType
    {
        /// <summary>EtherCAT 总线控制</summary>
        EtherCAT,
        /// <summary>API 直连电脑控制</summary>
        ApiDirect,
    }

    /// <summary>
    /// 设备轴枚举定义
    /// </summary>
    public enum AxisId
    {
        // ==================== 左工位 EtherCAT 总线轴 ====================
        /// <summary>左工位上相机模组X轴</summary>
        LeftCamUpX = 0,
        /// <summary>左工位上相机模组Y轴</summary>
        LeftCamUpY = 1,
        /// <summary>左工位上相机模组Z轴</summary>
        LeftCamUpZ = 2,
        /// <summary>左工位侧相机Y轴</summary>
        LeftCamSideY = 3,
        /// <summary>左工位左耦合θX轴</summary>
        LeftCouplingLThetaX = 4,
        /// <summary>左工位左耦合θY轴</summary>
        LeftCouplingLThetaY = 5,
        /// <summary>左工位左耦合θZ轴</summary>
        LeftCouplingLThetaZ = 6,
        /// <summary>左工位右耦合θX轴</summary>
        LeftCouplingRThetaX = 7,
        /// <summary>左工位右耦合θY轴</summary>
        LeftCouplingRThetaY = 8,
        /// <summary>左工位右耦合θZ轴</summary>
        LeftCouplingRThetaZ = 9,

        // ==================== 右工位 EtherCAT 总线轴 ====================
        /// <summary>右工位上相机模组X轴</summary>
        RightCamUpX = 10,
        /// <summary>右工位上相机模组Y轴</summary>
        RightCamUpY = 11,
        /// <summary>右工位上相机模组Z轴</summary>
        RightCamUpZ = 12,
        /// <summary>右工位侧相机Y轴</summary>
        RightCamSideY = 13,
        /// <summary>右工位左耦合θX轴</summary>
        RightCouplingLThetaX = 14,
        /// <summary>右工位左耦合θY轴</summary>
        RightCouplingLThetaY = 15,
        /// <summary>右工位左耦合θZ轴</summary>
        RightCouplingLThetaZ = 16,
        /// <summary>右工位右耦合θX轴</summary>
        RightCouplingRThetaX = 17,
        /// <summary>右工位右耦合θY轴</summary>
        RightCouplingRThetaY = 18,
        /// <summary>右工位右耦合θZ轴</summary>
        RightCouplingRThetaZ = 19,
    }

    /// <summary>
    /// 直线轴枚举定义（API 直连控制，精度 0.1μ，10000pul/r）
    /// </summary>
    public enum LinearAxisId
    {
        // ==================== 左工位 ====================
        /// <summary>左工位左耦合X轴</summary>
        LeftCouplingLX = 0,
        /// <summary>左工位左耦合Y轴</summary>
        LeftCouplingLY = 1,
        /// <summary>左工位左耦合Z轴</summary>
        LeftCouplingLZ = 2,
        /// <summary>左工位右耦合X轴</summary>
        LeftCouplingRX = 3,
        /// <summary>左工位右耦合Y轴</summary>
        LeftCouplingRY = 4,
        /// <summary>左工位右耦合Z轴</summary>
        LeftCouplingRZ = 5,

        // ==================== 右工位 ====================
        /// <summary>右工位左耦合X轴</summary>
        RightCouplingLX = 6,
        /// <summary>右工位左耦合Y轴</summary>
        RightCouplingLY = 7,
        /// <summary>右工位左耦合Z轴</summary>
        RightCouplingLZ = 8,
        /// <summary>右工位右耦合X轴</summary>
        RightCouplingRX = 9,
        /// <summary>右工位右耦合Y轴</summary>
        RightCouplingRY = 10,
        /// <summary>右工位右耦合Z轴</summary>
        RightCouplingRZ = 11,
    }

    /// <summary>
    /// 夹爪枚举定义（主从站控制）
    /// </summary>
    public enum GripperId
    {
        /// <summary>左工位左耦合夹爪</summary>
        LeftCouplingLGripper = 10,
        /// <summary>左工位右耦合夹爪</summary>
        LeftCouplingRGripper = 11,
        /// <summary>右工位左耦合夹爪</summary>
        RightCouplingLGripper = 22,
        /// <summary>右工位右耦合夹爪</summary>
        RightCouplingRGripper = 23,
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
