namespace AFOCS.Devices.BusAxisDevice;

/// <summary>
/// 设备轴枚举定义（EtherCAT 总线轴，共 20 轴）
/// </summary>
public enum BusAxisId
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