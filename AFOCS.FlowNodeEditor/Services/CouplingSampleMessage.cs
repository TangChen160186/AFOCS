using AFOCS.Infrastructure;

namespace AFOCS.FlowNodeEditor.Services;

/// <summary>耦合扫描采样消息类型</summary>
public enum CouplingSampleType
{
    /// <summary>扫描开始（曲线图应清空重绘）</summary>
    Start,

    /// <summary>单个采样点（含位置与各通道 RSP）</summary>
    Sample,

    /// <summary>扫描结束（含异常）</summary>
    End,
}

/// <summary>
/// 耦合扫描采样消息 —— 耦合节点执行期间通过 IEventAggregator 实时发布，供曲线图等 UI 订阅。
/// </summary>
public class CouplingSampleMessage
{
    /// <summary>当前工位（左/右），UI 据此路由到对应工位的曲线面板</summary>
    public WorkPos WorkPos { get; init; }

    /// <summary>消息类型</summary>
    public CouplingSampleType Type { get; init; }

    /// <summary>位置（脉冲），仅 Sample 有效</summary>
    public int Position { get; init; }

    /// <summary>通道号 → RSP 值（按通道号升序取前 N 个），仅 Sample 有效</summary>
    public IReadOnlyDictionary<int, double> ChannelRsp { get; init; } = new Dictionary<int, double>();
}
