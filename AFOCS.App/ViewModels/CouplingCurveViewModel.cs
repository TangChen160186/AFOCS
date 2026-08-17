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

public interface ITxCouplingCurveLeftTool : ITool;

public interface ITxCouplingCurveRightTool : ITool;

/// <summary>
/// 耦合曲线工具基类：按工位 + 来源（RX/TX）订阅耦合采样消息，
/// 把各通道数值曲线实时绘制到 ScottPlot。RX 值为 ISP 板 RSP，TX 值为雅克贝斯控制器光功率。
/// </summary>
public abstract class CouplingCurveViewModelBase : Tool, IHandle<CouplingSampleMessage>
{
    private readonly WorkPos _workPos;
    private readonly CouplingSource _source;
    private readonly ILogger _logger;

    /// <summary>曲线图对象（View 加载时通过 WpfPlot.Reset 挂接）</summary>
    public Plot Plot { get; } = new();

    /// <summary>数据更新后触发，View 用于调用 Refresh</summary>
    public event System.Action? PlotUpdated;

    // 每通道曲线数据（X=位置脉冲，Y=数值）
    private readonly Dictionary<int, List<double>> _xs = [];
    private readonly Dictionary<int, List<double>> _ys = [];
    private readonly Dictionary<int, ScottPlot.Plottables.Scatter> _series = [];

    protected CouplingCurveViewModelBase(WorkPos workPos, CouplingSource source, ILogger logger, IEventAggregator events)
    {
        _workPos = workPos;
        _source = source;
        _logger = logger;

        string posText = workPos == WorkPos.Left ? "左工位" : "右工位";
        string srcText = source == CouplingSource.Rx ? "RX" : "TX";
        Plot.Title($"{posText}{srcText}耦合曲线");
        Plot.Axes.Bottom.Label.Text = "位置 (脉冲)";
        Plot.Axes.Left.Label.Text = "数值";
        Plot.Font.Automatic();
        Plot.Legend.IsVisible = true;

        events.SubscribeOnUIThread(this);
    }

    public Task HandleAsync(CouplingSampleMessage message, CancellationToken cancellationToken)
    {
        if (message.WorkPos != _workPos || message.Source != _source)
            return Task.CompletedTask;

        switch (message.Type)
        {
            case CouplingSampleType.Start:
                _xs.Clear();
                _ys.Clear();
                _series.Clear();
                Plot.Clear();
                if (!string.IsNullOrEmpty(message.ValueLabel))
                    Plot.Axes.Left.Label.Text = message.ValueLabel;
                break;

            case CouplingSampleType.Sample:
                AppendSample(message);
                break;

            case CouplingSampleType.End:
                _logger.Information("耦合曲线[{WorkPos}/{Source}] 扫描结束，共 {Count} 点",
                    _workPos, _source, _xs.Values.FirstOrDefault()?.Count ?? 0);
                break;
        }

        PlotUpdated?.Invoke();
        return Task.CompletedTask;
    }

    private void AppendSample(CouplingSampleMessage message)
    {
        foreach (var (channel, value) in message.ChannelValues)
        {
            if (!_xs.TryGetValue(channel, out var xs))
            {
                xs = [];
                _xs[channel] = xs;
                _ys[channel] = [];
            }
            xs.Add(message.Position);
            _ys[channel].Add(value);

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
    : CouplingCurveViewModelBase(WorkPos.Left, CouplingSource.Rx, logger, events), IRxCouplingCurveLeftTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 500;
    public override double PreferredHeight => 300;
    public override string DisplayName => "左工位RX耦合曲线";
}

[Export]
[Export(typeof(IRxCouplingCurveRightTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class RxCouplingCurveRightViewModel(ILogger logger, IEventAggregator events)
    : CouplingCurveViewModelBase(WorkPos.Right, CouplingSource.Rx, logger, events), IRxCouplingCurveRightTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 500;
    public override double PreferredHeight => 300;
    public override string DisplayName => "右工位RX耦合曲线";
}

[Export]
[Export(typeof(ITxCouplingCurveLeftTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class TxCouplingCurveLeftViewModel(ILogger logger, IEventAggregator events)
    : CouplingCurveViewModelBase(WorkPos.Left, CouplingSource.Tx, logger, events), ITxCouplingCurveLeftTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 500;
    public override double PreferredHeight => 300;
    public override string DisplayName => "左工位TX耦合曲线";
}

[Export]
[Export(typeof(ITxCouplingCurveRightTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class TxCouplingCurveRightViewModel(ILogger logger, IEventAggregator events)
    : CouplingCurveViewModelBase(WorkPos.Right, CouplingSource.Tx, logger, events), ITxCouplingCurveRightTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 500;
    public override double PreferredHeight => 300;
    public override string DisplayName => "右工位TX耦合曲线";
}
