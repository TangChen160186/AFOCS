using AFOCS.Infrastructure;

namespace AFOCS.Devices.IspBoard;

/// <summary>
/// ISP Board 初始化结果
/// </summary>
public class IspInitResult
{
    /// <summary>应用名称列表</summary>
    public string[] AppNames { get; init; } = [];
    /// <summary>VISA 设备地址列表</summary>
    public string[] DeviceVisaAddresses { get; init; } = [];
}

/// <summary>
/// 单个通道的 RSP 值
/// </summary>
public readonly record struct RspData(WorkPos WorkPos, int Channel, double RspValue);

/// <summary>
/// 单个通道的 MPD 值
/// </summary>
public readonly record struct MpdData(WorkPos WorkPos, int Channel, double MpdInValue, double MpdOutValue);

/// <summary>
/// IPSN 轮询数据
/// </summary>
public readonly record struct IpsnData(WorkPos WorkPos, string Text);

/// <summary>
/// ISP Board 设备接口 —— 通过 ISPBoard.dll (C++ wrapper) 与 NI-VISA 硬件通信。
/// 单一设备实例，内部管理左右两个工位。
/// </summary>
public interface IIspBoardDevice : IDevice
{
    /// <summary>
    /// 初始化 ISP Board，加载产品配置文件
    /// </summary>
    Task<Result<IspInitResult>> InitializeAsync(string productCfgFilePath, CancellationToken token = default);

    /// <summary>
    /// DUT 寄存器读写
    /// </summary>
    Task<Result<ushort[]>> DutReadWriteAsync(uint devIndex, byte dutSlot, byte dutChannel,
        string appName, byte operation, ushort[]? dataIn = null, ushort dataOutCount = 256);

    /// <summary>
    /// 公式计算
    /// </summary>
    Task<Result<double>> FormularCalcAsync(string appName, double[] dataIn);

    /// <summary>
    /// 加热器扫描
    /// </summary>
    Task<Result<(ushort[] MpdOutADC, ushort[] MpdInADC)>> HeaterScanAsync(
        uint devIndex, byte dutSlot, byte dutChannel,
        string appName, ushort[] dataIn, ushort mpdOutCount = 256, ushort mpdInCount = 256);

    /// <summary>
    /// 实时读取指定工位的最新 RSP 值（不经轮询缓存，直接读写 DUT 并计算）
    /// </summary>
    Task<Result<RspData[]>> ReadRspAsync(WorkPos workPos, CancellationToken token = default);

    /// <summary>
    /// RSP 数据更新事件，每次轮询完成后触发，包含左右工位所有通道的 RSP 计算值
    /// </summary>
    event EventHandler<RspData[]>? RspDataUpdated;

    /// <summary>
    /// MPD 数据更新事件，每次轮询完成后触发，包含左右工位所有通道的 MPD_IN/MPD_OUT 值
    /// </summary>
    event EventHandler<MpdData[]>? MpdDataUpdated;

    /// <summary>
    /// IPSN 数据更新事件
    /// </summary>
    event EventHandler<IpsnData>? IpsnDataUpdated;
}
