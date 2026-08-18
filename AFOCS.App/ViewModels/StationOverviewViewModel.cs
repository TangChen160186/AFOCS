using System.ComponentModel.Composition;
using AFOCS.Framework.Framework;

namespace AFOCS.App.ViewModels;

/// <summary>
/// 左工位总览窗口：固定 3 行 2 列布局，集成流程节点监视、上下相机实时图像、
/// RSP/MPD 监控以及 RX/TX 耦合曲线，不再需要用户逐个打开各工具面板。
/// </summary>
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class LeftStationOverviewViewModel : WindowBase
{
    public LeftFlowMonitorViewModel FlowMonitor { get; }

    public LeftUpCameraViewModel UpCamera { get; }

    public LeftDownCameraViewModel DownCamera { get; }

    public LeftStationMonitorViewModel StationMonitor { get; }

    public RxCouplingCurveLeftViewModel RxCurve { get; }

    public TxCouplingCurveLeftViewModel TxCurve { get; }

    [ImportingConstructor]
    public LeftStationOverviewViewModel(
        LeftFlowMonitorViewModel flowMonitor,
        LeftUpCameraViewModel upCamera,
        LeftDownCameraViewModel downCamera,
        LeftStationMonitorViewModel stationMonitor,
        RxCouplingCurveLeftViewModel rxCurve,
        TxCouplingCurveLeftViewModel txCurve)
    {
        FlowMonitor = flowMonitor;
        UpCamera = upCamera;
        DownCamera = downCamera;
        StationMonitor = stationMonitor;
        RxCurve = rxCurve;
        TxCurve = txCurve;
        DisplayName = "左工位总览";
    }
}

/// <summary>
/// 右工位总览窗口：与左工位对称，固定 3 行 2 列布局集成各监控组件。
/// </summary>
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class RightStationOverviewViewModel : WindowBase
{
    public RightFlowMonitorViewModel FlowMonitor { get; }

    public RightUpCameraViewModel UpCamera { get; }

    public RightDownCameraViewModel DownCamera { get; }

    public RightStationMonitorViewModel StationMonitor { get; }

    public RxCouplingCurveRightViewModel RxCurve { get; }

    public TxCouplingCurveRightViewModel TxCurve { get; }

    [ImportingConstructor]
    public RightStationOverviewViewModel(
        RightFlowMonitorViewModel flowMonitor,
        RightUpCameraViewModel upCamera,
        RightDownCameraViewModel downCamera,
        RightStationMonitorViewModel stationMonitor,
        RxCouplingCurveRightViewModel rxCurve,
        TxCouplingCurveRightViewModel txCurve)
    {
        FlowMonitor = flowMonitor;
        UpCamera = upCamera;
        DownCamera = downCamera;
        StationMonitor = stationMonitor;
        RxCurve = rxCurve;
        TxCurve = txCurve;
        DisplayName = "右工位总览";
    }
}