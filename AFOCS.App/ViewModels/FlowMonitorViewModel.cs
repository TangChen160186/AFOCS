using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using AFOCS.Infrastructure;
using Caliburn.Micro;
using Serilog;

namespace AFOCS.App.ViewModels;

public interface ILeftFlowMonitorTool : ITool;

public interface IRightFlowMonitorTool : ITool;

/// <summary>节点执行记录：显示名（描述优先）、完成状态、执行耗时</summary>
public class NodeExecutionItem : PropertyChangedBase
{
    public string DisplayName { get; }

    public bool IsSuccess { get; }

    public string ElapsedText { get; }

    /// <summary>完成时刻</summary>
    public string TimeText { get; }

    public NodeExecutionItem(NodeExecutionMessage msg)
    {
        // 有描述优先显示描述，否则显示节点名称
        DisplayName = string.IsNullOrWhiteSpace(msg.NodeDescription) ? msg.NodeTitle : msg.NodeDescription;
        IsSuccess = msg.IsSuccess;
        ElapsedText = $"{msg.ElapsedMs} ms";
        TimeText = DateTime.Now.ToString("HH:mm:ss");
    }
}

/// <summary>
/// 工位流程监控面板基类：订阅 <see cref="NodeExecutionMessage"/>，
/// 按工位过滤后以"最新在上"的列表显示节点执行结果（名称、状态圆圈、耗时）。
/// </summary>
public abstract class FlowMonitorViewModelBase : Tool, IHandle<NodeExecutionMessage>, IHandle<FlowExecutionStartedMessage>
{
    private const int MaxItems = 200;

    private readonly WorkPos _workPos;
    private readonly ILogger _logger;

    public ObservableCollection<NodeExecutionItem> Items { get; } = [];

    protected FlowMonitorViewModelBase(WorkPos workPos, string displayName, IEventAggregator events, ILogger logger)
    {
        _workPos = workPos;
        _logger = logger;
        DisplayName = displayName;

        events.SubscribeOnUIThread(this);
    }

    /// <summary>新一轮流程开始：清空上一轮记录</summary>
    public Task HandleAsync(FlowExecutionStartedMessage message, CancellationToken cancellationToken)
    {
        if (message.WorkPos == _workPos)
            Items.Clear();

        return Task.CompletedTask;
    }

    public Task HandleAsync(NodeExecutionMessage message, CancellationToken cancellationToken)
    {
        if (message.WorkPos != _workPos)
            return Task.CompletedTask;

        Items.Insert(0, new NodeExecutionItem(message));
        while (Items.Count > MaxItems)
            Items.RemoveAt(Items.Count - 1);

        return Task.CompletedTask;
    }
}

// ==================== 左右工位流程监控面板 ====================

[Export]
[Export(typeof(ILeftFlowMonitorTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class LeftFlowMonitorViewModel(IEventAggregator events, ILogger logger)
    : FlowMonitorViewModelBase(WorkPos.Left, "左工位流程监控", events, logger), ILeftFlowMonitorTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Left;
    public override double PreferredWidth => 420;
    public override double PreferredHeight => 480;
}

[Export]
[Export(typeof(IRightFlowMonitorTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class RightFlowMonitorViewModel(IEventAggregator events, ILogger logger)
    : FlowMonitorViewModelBase(WorkPos.Right, "右工位流程监控", events, logger), IRightFlowMonitorTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Left;
    public override double PreferredWidth => 420;
    public override double PreferredHeight => 480;
}
