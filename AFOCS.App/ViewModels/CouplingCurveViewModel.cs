using System.Collections.ObjectModel;
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

    /// <summary>曲线已整合进工位总览窗口，不再作为独立工具在启动时重新打开</summary>
    public override bool ShouldReopenOnStart => false;

    /// <summary>各通道显示开关（固定数量：RX 4 个、TX 8 个）</summary>
    public ObservableCollection<ChannelToggleItem> ChannelToggles { get; } = [];

    private readonly Dictionary<int, ChannelToggleItem> _toggleByChannel = [];

    // 每通道曲线数据（X=位置脉冲，Y=数值）
    private readonly Dictionary<int, List<double>> _xs = [];
    private readonly Dictionary<int, List<double>> _ys = [];
    private readonly Dictionary<int, ScottPlot.Plottables.Scatter> _series = [];

    protected CouplingCurveViewModelBase(WorkPos workPos, CouplingSource source, int firstChannel, int channelCount, ILogger logger, IEventAggregator events)
    {
        _workPos = workPos;
        _source = source;
        _logger = logger;

        Plot.Axes.Bottom.Label.Text = "pos (pulse)";
        Plot.Axes.Bottom.Label.FontSize = 9;
        Plot.Axes.Left.Label.Text = source == CouplingSource.Rx ? "RSP" : "Power";
        Plot.Axes.Left.Label.FontSize = 9;
        Plot.Axes.Bottom.TickLabelStyle.FontSize = 9;
        Plot.Axes.Left.TickLabelStyle.FontSize = 9;
        Plot.Font.Automatic();
        Plot.Legend.IsVisible = false;
        Plot.Legend.FontSize = 10;

        // 固定生成通道显示开关（RX 4 个 0~3，TX 8 个 1~8），打开面板即可勾选
        for (int i = 0; i < channelCount; i++)
            AddToggle(firstChannel + i);

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
        }

        foreach (var channel in message.ChannelValues.Keys)
        {
            if (_toggleByChannel.TryGetValue(channel, out var item) && item.IsVisible)
                UpdateSeries(channel);
        }

        Plot.Axes.AutoScale();
    }

    /// <summary>根据通道显示开关增删曲线</summary>
    private void UpdateSeries(int channel)
    {
        bool visible = _toggleByChannel.TryGetValue(channel, out var item) && item.IsVisible;
        bool hasData = _xs.TryGetValue(channel, out var xs) && xs.Count > 0;

        if (_series.Remove(channel, out var old))
            Plot.Remove(old);

        if (visible && hasData)
        {
            // Scatter.Data 只读，数据量小，每次重建曲线更新
            var scatter = Plot.Add.Scatter(xs!.ToArray(), _ys[channel].ToArray());
            scatter.LegendText = $"CH{channel}";
            _series[channel] = scatter;
        }

        Plot.Axes.AutoScale();
        PlotUpdated?.Invoke();
    }

    private void AddToggle(int channel)
    {
        var item = new ChannelToggleItem(channel);
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChannelToggleItem.IsVisible))
                UpdateSeries(channel);
        };
        _toggleByChannel[channel] = item;
        ChannelToggles.Add(item);
    }
}

/// <summary>单通道显示开关（CheckBox 数据源）</summary>
public class ChannelToggleItem : PropertyChangedBase
{
    public int Channel { get; }

    public string Display => $"CH{Channel}";

    private bool _isVisible = true;

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    public ChannelToggleItem(int channel)
    {
        Channel = channel;
    }
}

[Export]
[Export(typeof(IRxCouplingCurveLeftTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class RxCouplingCurveLeftViewModel(ILogger logger, IEventAggregator events)
    : CouplingCurveViewModelBase(WorkPos.Left, CouplingSource.Rx, 0, 4, logger, events), IRxCouplingCurveLeftTool
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
    : CouplingCurveViewModelBase(WorkPos.Right, CouplingSource.Rx, 0, 4, logger, events), IRxCouplingCurveRightTool
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
    : CouplingCurveViewModelBase(WorkPos.Left, CouplingSource.Tx, 1, 8, logger, events), ITxCouplingCurveLeftTool
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
    : CouplingCurveViewModelBase(WorkPos.Right, CouplingSource.Tx, 1, 8, logger, events), ITxCouplingCurveRightTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 500;
    public override double PreferredHeight => 300;
    public override string DisplayName => "右工位TX耦合曲线";
}
