using AFOCS.Infrastructure;

namespace AFOCS.Devices.IspBoard;

/// <summary>
/// ISP Board 配置
/// </summary>
///
[ConfigPath("设备/IspBoard")]
public class IspBoardConfig
{
    /// <summary>产品配置文件路径</summary>
    public string ProductCfgFilePath { get; set; } = "ProductCfg.json";
    public string IpsnAppName { get; set; } = "IPSN";

    public string RxAdcAppName { get; set; } = "RxADC";
    public string RxAdcFormulaAppName { get; set; } = "RxADC_R";

    public string MpdInAppName { get; set; } = "MPDInADC";
    public string MpdInCoeffAppName { get; set; } = "MPDInADCCoeff";

    public string MpdOutAppName { get; set; } = "MPDOutADC";
    public string MpdOutCoeffAppName { get; set; } = "MPDOutADCCoeff";

    public int DeviceId { get; set; } = 0;
    public int DutSlot { get; set; } = 1;

    public int DutChannel { get; set; } = 0;
}