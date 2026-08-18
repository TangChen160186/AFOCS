using AFOCS.Infrastructure;

namespace AFOCS.FlowNodeEditor.Services;

/// <summary>
/// 流程执行开始消息 —— 通过 IEventAggregator 发布，供订阅方清空上一轮记录
/// </summary>
public class FlowExecutionStartedMessage
{
    /// <summary>当前工位</summary>
    public WorkPos WorkPos { get; init; }
}

/// <summary>
/// 流程执行完成消息 —— 整条流程执行结束（成功或失败）时由 IFlowExecutionService 通过 IEventAggregator 发布，
/// 供总览窗口等订阅方统计良品 / 不良个数。
/// </summary>
public class FlowExecutionCompletedMessage
{
    /// <summary>当前工位</summary>
    public WorkPos WorkPos { get; init; }

    /// <summary>整条流程是否执行成功（无异常）</summary>
    public bool Success { get; init; }

    /// <summary>错误信息（失败时有值）</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 节点执行结果消息 —— 通过 IEventAggregator 发布，供外部订阅
/// </summary>
public class NodeExecutionMessage
{
    /// <summary>当前工位</summary>
    public WorkPos WorkPos { get; init; }

    /// <summary>节点标题（DisplayName）</summary>
    public string NodeTitle { get; init; } = string.Empty;

    /// <summary>节点描述（可能为空）</summary>
    public string NodeDescription { get; init; } = string.Empty;

    /// <summary>节点 TypeId</summary>
    public string NodeTypeId { get; init; } = string.Empty;

    /// <summary>执行是否成功</summary>
    public bool IsSuccess { get; init; }

    /// <summary>错误信息（失败时有值）</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>执行耗时（毫秒）</summary>
    public long ElapsedMs { get; init; }
}
