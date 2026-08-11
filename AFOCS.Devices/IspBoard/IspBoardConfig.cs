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
    //public string RxAdcAppName { get; set; } = "RxADC";
    //public string MpdInAppName { get; set; } = "MPDInADC";
    //public string MpdInCoeffName { get; set; } = "MPDInADCCoeff";
    //public string MpdOutAppName { get; set; } = "MPDOutADC";
    //public string MpdOutCoeffAppName { get; set; } = "MPDOutADCCoeff";
    //public string IpsnAppName { get; set; } = "IPSN";
    //public string RxAdcFormulaAppName { get; set; } = "RxADC_R";
}