using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using AFOCS.App.Models;
using AFOCS.Devices.AkribrisMotion;
using AFOCS.Devices.BusAxisDevice;
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
    private readonly IBusAxisDevice _busAxisDevice;
    private readonly IIspBoardDevice _ispBoard;
    private readonly IConfigService _configService;
    private readonly IFlowExecutionService _flowExecutionService;
    private readonly IReadOnlyList<IAkribisMotion> _akribisMotions;

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

    /// <summary>流程是否正在执行（执行期间仅允许急停 / 取消）</summary>
    private bool _isFlowRunning;
    public bool IsFlowRunning
    {
        get => _isFlowRunning;
        private set
        {
            if (Set(ref _isFlowRunning, value))
            {
                NotifyOfPropertyChange(nameof(CanStart));
                NotifyOfPropertyChange(nameof(CanHome));
                NotifyOfPropertyChange(nameof(CanSafePosition));
            }
        }
    }

    public bool CanStart => !IsFlowRunning;
    public bool CanHome => !IsFlowRunning;
    public bool CanSafePosition => !IsFlowRunning;

    protected StationOverviewViewModelBase(
        WorkPos workPos,
        IBusAxisDevice busAxisDevice,
        IIspBoardDevice ispBoard,
        IEnumerable<IAkribisMotion> akribisMotions,
        IConfigService configService,
        IFlowExecutionService flowExecutionService,
        IEventAggregator events)
    {
        _workPos = workPos;
        _busAxisDevice = busAxisDevice;
        _ispBoard = ispBoard;
        _configService = configService;
        _flowExecutionService = flowExecutionService;
        _akribisMotions = akribisMotions as IReadOnlyList<IAkribisMotion> ?? akribisMotions.ToList();

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

    /// <summary>急停：紧急停止当前工位所有轴，并中止正在运行的流程</summary>
    public async Task EmergencyStop()
    {
        await StopAllAxesAsync(emergency: true);
        _flowExecutionService.CancelExecution(_workPos, emergency: true);
    }

    /// <summary>取消：停止当前工位所有轴，并取消正在运行的流程</summary>
    public async Task Cancel()
    {
        await StopAllAxesAsync(emergency: false);
        _flowExecutionService.CancelExecution(_workPos, emergency: false);
    }

    /// <summary>停止当前工位所有轴（EtherCAT 总线轴 + 雅克贝斯直连轴），emergency=true 时使用急停模式</summary>
    private async Task StopAllAxesAsync(bool emergency)
    {
        // 1. EtherCAT 总线轴：左工位 0-9，右工位 10-19
        var start = _workPos == WorkPos.Left ? 0 : 10;
        var tasks = new List<Task>();
        for (var i = 0; i < 10; i++)
            tasks.Add(_busAxisDevice.StopAxisAsync((BusAxisId)(start + i), emergency));

        // 2. 雅克贝斯直连轴：当前工位的所有实例（每实例含 X/Y/Z 三轴）
        foreach (var motion in _akribisMotions.Where(m => m.WorkPos == _workPos))
            tasks.Add(emergency ? motion.EmergencyStopAllAsync() : motion.StopAxisAsync());

        await Task.WhenAll(tasks);
    }

    private async Task ExecuteConfiguredFlowAsync(Func<StationProductionInfo, string> pathSelector, bool reportResult)
    {
        var config = await _configService.LoadAsync<ProductionInfoConfig>();
        var info = _workPos == WorkPos.Left ? config?.Left : config?.Right;
        if (info == null)
            return;

        var path = pathSelector(info);
        if (string.IsNullOrWhiteSpace(path))
            return;

        IsFlowRunning = true;
        try
        {
            await _flowExecutionService.ExecuteFlowAsync(path, _workPos, reportResult);
        }
        finally
        {
            IsFlowRunning = false;
        }
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
        IBusAxisDevice busAxisDevice,
        IIspBoardDevice ispBoard,
        [ImportMany] IEnumerable<IAkribisMotion> akribisMotions,
        IConfigService configService,
        IFlowExecutionService flowExecutionService,
        IEventAggregator events)
        : base(WorkPos.Left, busAxisDevice, ispBoard, akribisMotions, configService, flowExecutionService, events)
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
        IBusAxisDevice busAxisDevice,
        IIspBoardDevice ispBoard,
        [ImportMany] IEnumerable<IAkribisMotion> akribisMotions,
        IConfigService configService,
        IFlowExecutionService flowExecutionService,
        IEventAggregator events)
        : base(WorkPos.Right, busAxisDevice, ispBoard, akribisMotions, configService, flowExecutionService, events)
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