using AFOCS.Infrastructure;

namespace AFOCS.Devices
{
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
    /// ISP Board 设备接口 —— 通过 ISPBoard.dll (C++ wrapper) 与 NI-VISA 硬件通信
    /// </summary>
    public interface IIspBoardDevice : IDevice
    {
        /// <summary>
        /// 初始化 ISP Board，加载产品配置文件
        /// </summary>
        /// <param name="productCfgFilePath">产品配置文件路径（JSON 内容字符串）</param>
        /// <returns>初始化结果（AppNames + DeviceVisa）</returns>
        Task<Result<IspInitResult>> InitializeAsync(string productCfgFilePath, CancellationToken token = default);

        /// <summary>
        /// 进入/退出工程模式
        /// </summary>
        /// <param name="devIndex">设备索引</param>
        /// <param name="enterEng">true=进入工程模式, false=退出工程模式</param>
        /// <returns>工程模式状态字节数组</returns>
        Task<Result<byte[]>> SetEngineeringModeAsync(uint devIndex, bool enterEng);

        /// <summary>
        /// DUT 寄存器读写
        /// </summary>
        /// <param name="devIndex">设备索引</param>
        /// <param name="dutSlot">DUT 插槽号</param>
        /// <param name="dutChannel">DUT 通道号</param>
        /// <param name="appName">应用名称</param>
        /// <param name="operation">操作类型（0=读, 1=写, 其他=自定义）</param>
        /// <param name="dataIn">写入数据（写操作时有效）</param>
        /// <returns>读取到的数据</returns>
        Task<Result<ushort[]>> DutReadWriteAsync(uint devIndex, byte dutSlot, byte dutChannel,
            string appName, ushort operation, ushort[]? dataIn = null);

        /// <summary>
        /// 加热器扫描
        /// </summary>
        /// <param name="devIndex">设备索引</param>
        /// <param name="dutSlot">DUT 插槽号</param>
        /// <param name="dutChannel">DUT 通道号</param>
        /// <param name="appName">应用名称</param>
        /// <param name="dataIn">加热器控制参数（uint16 数组）</param>
        /// <returns>MpdOutADC 和 MpdInADC 数据</returns>
        Task<Result<(ushort[] MpdOutADC, ushort[] MpdInADC)>> HeaterScanAsync(
            uint devIndex, byte dutSlot, byte dutChannel,
            string appName, ushort[] dataIn);
    }
}
