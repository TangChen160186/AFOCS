using AFOCS.Infrastructure;

namespace AFOCS.Devices.MotionControlCard;

public interface IMotionControlCard : IDevice
{
    LeadShineMotionCardConfig GetConfig();
    Task SaveConfigAsync(LeadShineMotionCardConfig config);

    Task<Result> HotResetAsync(CancellationToken token = default);

    /// <summary>获取总线错误码和状态描述</summary>
    Task<Result<(ushort ErrorCode, string Description)>> GetBusStatusAsync();



    // ========== IO 读写 ==========

    Task<Result<bool>> ReadInbitAsync(ushort bitNo);
    Task<Result<bool[]>> ReadInbitsAsync(ushort bitCount);
    Task<Result<bool>> ReadOutbitAsync(ushort bitNo);
    Task<Result> WriteOutbitAsync(ushort bitNo, bool on);

    // ========== PDO 读写 ==========

    Task<Result> WriteRxPDOAsync(ushort slaveAddr, ushort index, ushort subIndex, ushort bitLength, int value);
    Task<Result<int>> ReadTxPDOAsync(ushort slaveAddr, ushort index, ushort subIndex, ushort bitLength);

    // ========== 底层轴操作（薄封装，供 BusAxisDevice 调用） ==========

    Task<Result<double>> GetPositionAsync(ushort axis);
    Task<Result<double>> GetSpeedAsync(ushort axis);
    Task<Result> SetEquivAsync(ushort axis, double equiv);
    Task<Result> SetProfileUnitAsync(ushort axis, double minVel, double maxVel, double tacc, double tdec, double stopVel);
    Task<Result> SetSProfileAsync(ushort axis, ushort mode, double sPara);
    Task<Result> PmoveUnitAsync(ushort axis, double distance, ushort posiMode);
    Task<Result<int>> CheckDoneAsync(ushort axis);
    Task<Result<uint>> GetAxisIoStatusAsync(ushort axis);
    Task<Result<ushort>> GetAxisStateMachineAsync(ushort axis);
    Task<Result> SetHomeProfileAsync(ushort axis, ushort homeMode, double lowVel, double highVel, double tacc, double tdec, double offsetPos);
    Task<Result> HomeMoveAsync(ushort axis);
    Task<Result<ushort>> GetHomeResultAsync(ushort axis);
    Task<Result<int>> GetStopReasonAsync(ushort axis);
    Task<Result> EnableAxisAsync(ushort axis, int timeoutMs = 3000);
    Result DisableAxis(ushort axis);
    Task<Result> EmergencyStopAllAsync();
    Task<Result> SetSoftLimitAsync(ushort axis, double negativeLimit, double positiveLimit, bool enable = true);
    Task<Result> StopAxisAsync(ushort axis, bool emergency = false);

    // ========== 插补 ==========

    Task<Result> SetVectorProfileUnitAsync(ushort crd, double minVel, double maxVel, double tacc, double tdec, double stopVel);
    Task<Result> SetVectorSProfileAsync(ushort crd, ushort mode, double sPara);
    Task<Result> LineUnitAsync(ushort crd, ushort axisCount, ushort[] axisList, double[] targetPositions, ushort posiMode);
    Task<Result<int>> CheckDoneMultiCoorAsync(ushort crd);
    Task<Result> StopMultiCoorAsync(ushort crd, ushort mode);
}