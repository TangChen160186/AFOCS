using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using AFOCS.Infrastructure;
using Caliburn.Micro;
using ScottPlot;
using Serilog;

namespace AFOCS.App.ViewModels;

public interface IRxCouplingCurveLeftTool : ITool;

public interface IRxCouplingCurveRightTool : ITool;

/// <summary>
/// RX 耦合曲线工具基类：订阅耦合采样消息，把各通道 RSP 曲线实时绘制到 ScottPlot。
/// 子类通过构造函数参数固定工位（左/右），分别注册为两个独立工具面板。
/// </summary>
public abstract class RxCouplingCurveViewModelBase : Tool, IHandle<CouplingSampleMessage>
{
    private readonly WorkPos _workPos;
    private readonly ILogger _logger;

    /// <summary>曲线图对象（View 加载时通过 WpfPlot.Reset 挂接）</summary>
    public Plot Plot { get; } = new();

    /// <summary>数据更新后触发，View 用于调用 Refresh</summary>
    public event System.Action? PlotUpdated;

    // 每通道曲线数据（X=位置脉冲，Y=RSP）
    private readonly Dictionary<int, List<double>> _xs = [];
    private readonly Dictionary<int, List<double>> _ys = [];
    private readonly Dictionary<int, ScottPlot.Plottables.Scatter> _series = [];

    protected RxCouplingCurveViewModelBase(WorkPos workPos, ILogger logger, IEventAggregator events)
    {
        _workPos = workPos;
        _logger = logger;

        Plot.Title(workPos == WorkPos.Left ? "左工位 RX 耦合曲线" : "右工位 RX 耦合曲线");
        Plot.Axes.Bottom.Label.Text = "位置 (脉冲)";
        Plot.Axes.Left.Label.Text = "RSP";
        Plot.Font.Automatic();
        Plot.Legend.IsVisible = true;

        events.SubscribeOnUIThread(this);
    }

    public Task HandleAsync(CouplingSampleMessage message, CancellationToken cancellationToken)
    {
        if (message.WorkPos != _workPos)
            return Task.CompletedTask;

        switch (message.Type)
        {
            case CouplingSampleType.Start:
                _xs.Clear();
                _ys.Clear();
                _series.Clear();
                Plot.Clear();
                break;

            case CouplingSampleType.Sample:
                AppendSample(message);
                break;

            case CouplingSampleType.End:
                _logger.Information("RX耦合曲线[{WorkPos}] 扫描结束，共 {Count} 点", _workPos, _xs.Values.FirstOrDefault()?.Count ?? 0);
                break;
        }

        PlotUpdated?.Invoke();
        return Task.CompletedTask;
    }

    private void AppendSample(CouplingSampleMessage message)
    {
        foreach (var (channel, rsp) in message.ChannelRsp)
        {
            if (!_xs.TryGetValue(channel, out var xs))
            {
                xs = [];
                _xs[channel] = xs;
                _ys[channel] = [];
            }
            xs.Add(message.Position);
            _ys[channel].Add(rsp);

            // Scatter.Data 只读，数据量小（每通道约 21 点），每次重建曲线更新
            if (_series.Remove(channel, out var old))
                Plot.Remove(old);

            var scatter = Plot.Add.Scatter(xs.ToArray(), _ys[channel].ToArray());
            scatter.LegendText = $"CH{channel}";
            _series[channel] = scatter;
        }

        Plot.Axes.AutoScale();
    }
}

[Export]
[Export(typeof(IRxCouplingCurveLeftTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class RxCouplingCurveLeftViewModel(ILogger logger, IEventAggregator events)
    : RxCouplingCurveViewModelBase(WorkPos.Left, logger, events), IRxCouplingCurveLeftTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 500;
    public override double PreferredHeight => 300;
    public override string DisplayName => "左工位耦合曲线";
}

[Export]
[Export(typeof(IRxCouplingCurveRightTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class RxCouplingCurveRightViewModel(ILogger logger, IEventAggregator events)
    : RxCouplingCurveViewModelBase(WorkPos.Right, logger, events), IRxCouplingCurveRightTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 500;
    public override double PreferredHeight => 300;
    public override string DisplayName => "右工位耦合曲线";
}
