using AFOCS.Infrastructure;

namespace AFOCS.Devices.IspBoard;

/// <summary>
/// 工位独立配置
/// </summary>
public class WorkstationConfig
{
    public int DeviceId { get; set; } = 0;
    public int DutSlot { get; set; } = 1;
    public int DutChannel { get; set; } = 0;

    /// <summary>各通道的光功率参考值，用于 RSP 公式计算</summary>
    public double[] ChannelLight { get; set; } = [1, 1, 1, 1, 1, 1, 1, 1];
}

/// <summary>
/// ISP Board 配置（单一文件，包含左右工位）
/// </summary>
[ConfigPath("设备/ISP/IspBoard")]
public class IspBoardConfig
{
    /// <summary>产品配置文件路径</summary>
    public string ProductCfgFilePath { get; set; } = "D:\\dll\\R50008 800G DR8 12-9 (mpd Formula change)_Encrypt.ini";

    public string IpsnAppName { get; set; } = "IPSN";
    public string RxAdcAppName { get; set; } = "RxADC";
    public string RxAdcFormulaAppName { get; set; } = "RxADC_R";
    public string MpdInAppName { get; set; } = "MPDInADC";
    public string MpdOutAppName { get; set; } = "MPDOutADC";

    /// <summary>左工位参数</summary>
    public WorkstationConfig Left { get; set; } = new(){DeviceId = 0};

    /// <summary>右工位参数</summary>
    public WorkstationConfig Right { get; set; } = new(){DeviceId = 1};

    /// <summary>RSP 轮询间隔（毫秒），默认 200ms</summary>
    public int RspPollingIntervalMs { get; set; } = 200;
}
