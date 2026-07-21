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
        //Task<Result<string>> GetIpsn();
        
        /// <summary>
        /// 初始化 ISP Board，加载产品配置文件
        /// </summary>
        /// <param name="productCfgFilePath">产品配置文件路径</param>
        /// <returns>初始化结果（AppNames + DeviceVisa）</returns>
        Task<Result<IspInitResult>> InitializeAsync(string productCfgFilePath, CancellationToken token = default);

        /// <summary>
        /// DUT 寄存器读写
        /// </summary>
        /// <param name="devIndex">设备索引</param>
        /// <param name="dutSlot">DUT 插槽号</param>
        /// <param name="dutChannel">DUT 通道号</param>
        /// <param name="appName">应用名称</param>
        /// <param name="operation">操作类型（0=读, 1=写）</param>
        /// <param name="dataIn">写入数据（写操作时有效）</param>
        /// <param name="dataOutCount">输出缓冲区大小（元素个数）</param>
        /// <returns>读取到的数据</returns>
        Task<Result<ushort[]>> DutReadWriteAsync(uint devIndex, byte dutSlot, byte dutChannel,
            string appName, byte operation, ushort[]? dataIn = null, ushort dataOutCount = 256);

        /// <summary>
        /// 公式计算
        /// </summary>
        /// <param name="appName">应用名称</param>
        /// <param name="dataIn">输入数据（double 数组）</param>
        /// <returns>计算结果</returns>
        Task<Result<double>> FormularCalcAsync(string appName, double[] dataIn);

        /// <summary>
        /// 加热器扫描
        /// </summary>
        /// <param name="devIndex">设备索引</param>
        /// <param name="dutSlot">DUT 插槽号</param>
        /// <param name="dutChannel">DUT 通道号</param>
        /// <param name="appName">应用名称</param>
        /// <param name="dataIn">加热器控制参数</param>
        /// <param name="mpdOutCount">mpdOutAdc 缓冲区大小</param>
        /// <param name="mpdInCount">mpdInAdc 缓冲区大小</param>
        /// <returns>MpdOutADC 和 MpdInADC 数据</returns>
        Task<Result<(ushort[] MpdOutADC, ushort[] MpdInADC)>> HeaterScanAsync(
            uint devIndex, byte dutSlot, byte dutChannel,
            string appName, ushort[] dataIn, ushort mpdOutCount = 256, ushort mpdInCount = 256);
    }
}
