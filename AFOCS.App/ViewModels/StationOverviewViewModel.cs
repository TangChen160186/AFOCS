using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using AFOCS.App.Models;
using AFOCS.Devices.IspBoard;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Framework.Framework;
using AFOCS.Infrastructure;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels;

/// <summary>
/// 工位总览生产信息面板基类：统计良品 / 不良个数（订阅流程执行完成消息）、
/// 显示 IPSN（订阅 ISP Board 事件）与胶水 SN（读取生产信息配置）。
/// </summary>
public abstract class StationOverviewViewModelBase : WindowBase, IHandle<FlowExecutionCompletedMessage>
{
    private readonly WorkPos _workPos;
    private readonly IIspBoardDevice _ispBoard;
    private readonly IConfigService _configService;
    private readonly IFlowExecutionService _flowExecutionService;

    private int _goodCount;
    public int GoodCount
    {
        get => _goodCount;
        private set => Set(ref _goodCount, value);
    }

    private int _badCount;
    public int BadCount
    {
        get => _badCount;
        private set => Set(ref _badCount, value);
    }

    private string _ipsn = string.Empty;
    public string Ipsn
    {
        get => _ipsn;
        private set => Set(ref _ipsn, value);
    }

    public string GlueSn { get; private set; } = string.Empty;

    protected StationOverviewViewModelBase(
        WorkPos workPos,
        IIspBoardDevice ispBoard,
        IConfigService configService,
        IFlowExecutionService flowExecutionService,
        IEventAggregator events)
    {
        _workPos = workPos;
        _ispBoard = ispBoard;
        _configService = configService;
        _flowExecutionService = flowExecutionService;

        var config = Task.Run(() => configService.LoadAsync<ProductionInfoConfig>()).GetAwaiter().GetResult();
        GlueSn = workPos == WorkPos.Left
            ? config?.Left.GlueSn ?? string.Empty
            : config?.Right.GlueSn ?? string.Empty;

        _ispBoard.IpsnDataUpdated += OnIpsnDataUpdated;
        events.SubscribeOnUIThread(this);
    }

    private void OnIpsnDataUpdated(object? sender, IpsnData data)
    {
        if (data.WorkPos != _workPos)
            return;

        Execute.OnUIThread(() => Ipsn = data.Text);
    }

    public Task HandleAsync(FlowExecutionCompletedMessage message, CancellationToken cancellationToken)
    {
        if (message.WorkPos != _workPos)
            return Task.CompletedTask;

        if (message.Success)
            GoodCount++;
        else
            BadCount++;

        return Task.CompletedTask;
    }

    /// <summary>启动：执行配置的逻辑流程（计入良品 / 不良）</summary>
    public Task Start() => ExecuteConfiguredFlowAsync(i => i.LogicFlowPath, reportResult: true);

    /// <summary>回零：执行配置的回零流程（不计入良品 / 不良）</summary>
    public Task Home() => ExecuteConfiguredFlowAsync(i => i.HomeFlowPath, reportResult: false);

    /// <summary>安全位：执行配置的安全位置流程（不计入良品 / 不良）</summary>
    public Task SafePosition() => ExecuteConfiguredFlowAsync(i => i.SafePositionFlowPath, reportResult: false);

    private async Task ExecuteConfiguredFlowAsync(Func<StationProductionInfo, string> pathSelector, bool reportResult)
    {
        var config = await _configService.LoadAsync<ProductionInfoConfig>();
        var info = _workPos == WorkPos.Left ? config?.Left : config?.Right;
        if (info == null)
            return;

        var path = pathSelector(info);
        if (string.IsNullOrWhiteSpace(path))
            return;

        await _flowExecutionService.ExecuteFlowAsync(path, _workPos, reportResult);
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        _ispBoard.IpsnDataUpdated -= OnIpsnDataUpdated;
        return base.OnDeactivateAsync(close, cancellationToken);
    }
}

/// <summary>
/// 左工位总览窗口：固定 3 行 2 列布局，集成流程节点监视、上下相机实时图像、
/// RSP/MPD 监控以及 RX/TX 耦合曲线，不再需要用户逐个打开各工具面板。
/// </summary>
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class LeftStationOverviewViewModel : StationOverviewViewModelBase
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
        TxCouplingCurveLeftViewModel txCurve,
        IIspBoardDevice ispBoard,
        IConfigService configService,
        IFlowExecutionService flowExecutionService,
        IEventAggregator events)
        : base(WorkPos.Left, ispBoard, configService, flowExecutionService, events)
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
public class RightStationOverviewViewModel : StationOverviewViewModelBase
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
        TxCouplingCurveRightViewModel txCurve,
        IIspBoardDevice ispBoard,
        IConfigService configService,
        IFlowExecutionService flowExecutionService,
        IEventAggregator events)
        : base(WorkPos.Right, ispBoard, configService, flowExecutionService, events)
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