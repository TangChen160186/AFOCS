using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using AFOCS.Devices.IspBoard;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using AFOCS.Infrastructure;
using Caliburn.Micro;
using Serilog;

namespace AFOCS.App.ViewModels;

public interface ILeftStationMonitorTool : ITool;

public interface IRightStationMonitorTool : ITool;

/// <summary>工位监控行：单个通道的 RSP / MPD_IN / MPD_OUT 值</summary>
public class ChannelValue : PropertyChangedBase
{
    public int Channel { get; }

    private double _rsp;
    public double Rsp
    {
        get => _rsp;
        set => Set(ref _rsp, value);
    }

    private double _mpdIn;
    public double MpdIn
    {
        get => _mpdIn;
        set => Set(ref _mpdIn, value);
    }

    private double _mpdOut;
    public double MpdOut
    {
        get => _mpdOut;
        set => Set(ref _mpdOut, value);
    }

    public ChannelValue(int channel) => Channel = channel;
}

/// <summary>
/// 工位 ISP 数据监控面板基类：订阅 ISP Board 轮询事件，
/// 按工位过滤后以"通道竖排"的表格显示 RSP、MPD_IN、MPD_OUT。
/// </summary>
public abstract class IspBoardMonitorViewModelBase : Tool
{
    private readonly IIspBoardDevice _ispBoard;
    private readonly WorkPos _workPos;
    private readonly ILogger _logger;

    public ObservableCollection<ChannelValue> Channels { get; } = [];

    protected IspBoardMonitorViewModelBase(IIspBoardDevice ispBoard, WorkPos workPos, ILogger logger)
    {
        _ispBoard = ispBoard;
        _workPos = workPos;
        _logger = logger;
        DisplayName = workPos == WorkPos.Left ? "左工位监控" : "右工位监控";

        _ispBoard.RspDataUpdated += OnRspDataUpdated;
        _ispBoard.MpdDataUpdated += OnMpdDataUpdated;
    }

    private void OnRspDataUpdated(object? sender, RspData[] data)
    {
        var rows = data.Where(d => d.WorkPos == _workPos).ToArray();
        if (rows.Length == 0)
            return;

        Execute.OnUIThread(() =>
        {
            foreach (var r in rows)
                GetOrAddChannel(r.Channel).Rsp = r.RspValue;
        });
    }

    private void OnMpdDataUpdated(object? sender, MpdData[] data)
    {
        var rows = data.Where(d => d.WorkPos == _workPos).ToArray();
        if (rows.Length == 0)
            return;

        Execute.OnUIThread(() =>
        {
            foreach (var m in rows)
            {
                var channel = GetOrAddChannel(m.Channel);
                channel.MpdIn = m.MpdInValue;
                channel.MpdOut = m.MpdOutValue;
            }
        });
    }

    private ChannelValue GetOrAddChannel(int channel)
    {
        var existing = Channels.FirstOrDefault(c => c.Channel == channel);
        if (existing != null)
            return existing;

        var created = new ChannelValue(channel);
        Channels.Add(created);
        return created;
    }
}

// ==================== 左右工位监控面板 ====================

[Export]
[Export(typeof(ILeftStationMonitorTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class LeftStationMonitorViewModel(
    IIspBoardDevice ispBoard,
    ILogger logger)
    : IspBoardMonitorViewModelBase(ispBoard, WorkPos.Left, logger), ILeftStationMonitorTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Left;
    public override double PreferredWidth => 360;
    public override double PreferredHeight => 420;
}

[Export]
[Export(typeof(IRightStationMonitorTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class RightStationMonitorViewModel(
    IIspBoardDevice ispBoard,
    ILogger logger)
    : IspBoardMonitorViewModelBase(ispBoard, WorkPos.Right, logger), IRightStationMonitorTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Left;
    public override double PreferredWidth => 360;
    public override double PreferredHeight => 420;
}
