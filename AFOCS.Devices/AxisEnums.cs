namespace AFOCS.Devices
{
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
    /// 设备轴枚举定义（EtherCAT 总线轴，共 20 轴）
    /// </summary>
    public enum AxisId
    {
        // ==================== 左工位 EtherCAT 总线轴 ====================
        LeftCamUpX = 0,
        LeftCamUpY = 1,
        LeftCamUpZ = 2,
        LeftCamSideY = 3,
        LeftCouplingLThetaX = 4,
        LeftCouplingLThetaY = 5,
        LeftCouplingLThetaZ = 6,
        LeftCouplingRThetaX = 7,
        LeftCouplingRThetaY = 8,
        LeftCouplingRThetaZ = 9,

        // ==================== 右工位 EtherCAT 总线轴 ====================
        RightCamUpX = 10,
        RightCamUpY = 11,
        RightCamUpZ = 12,
        RightCamSideY = 13,
        RightCouplingLThetaX = 14,
        RightCouplingLThetaY = 15,
        RightCouplingLThetaZ = 16,
        RightCouplingRThetaX = 17,
        RightCouplingRThetaY = 18,
        RightCouplingRThetaZ = 19,
    }

    /// <summary>
    /// 直线轴枚举定义（API 直连控制，精度 0.1μ，10000pul/r）
    /// </summary>
    public enum LinearAxisId
    {
        LeftCouplingLX = 0,
        LeftCouplingLY = 1,
        LeftCouplingLZ = 2,

        LeftCouplingRX = 3,
        LeftCouplingRY = 4,
        LeftCouplingRZ = 5,

        RightCouplingLX = 6,
        RightCouplingLY = 7,
        RightCouplingLZ = 8,

        RightCouplingRX = 9,
        RightCouplingRY = 10,
        RightCouplingRZ = 11,
    }
}
