using AFOCS.Infrastructure;

namespace AFOCS.FlowNodeEditor.Services;

/// <summary>耦合扫描采样消息类型</summary>
public enum CouplingSampleType
{
    /// <summary>扫描开始（曲线图应清空重绘）</summary>
    Start,

    /// <summary>单个采样点（含位置与各通道 RSP），RX 等实时逐点发布时使用</summary>
    Sample,

    /// <summary>批量采样点（含完整位置序列与各通道数值序列），TX 等一次性返回数据时使用</summary>
    Batch,

    /// <summary>扫描结束（含异常）</summary>
    End,
}

/// <summary>耦合数据来源</summary>
public enum CouplingSource
{
    /// <summary>ISP 板 RSP（RX 耦合）</summary>
    Rx,

    /// <summary>雅克贝斯控制器光功率（TX 耦合）</summary>
    Tx,
}

/// <summary>
/// 耦合扫描采样消息 —— 耦合节点执行期间通过 IEventAggregator 实时发布，供曲线图等 UI 订阅。
/// 数值来源：RX 节点为 ISP 板 RSP，TX 节点为雅克贝斯控制器光功率。
/// </summary>
public class CouplingSampleMessage
{
    /// <summary>当前工位（左/右），UI 据此路由到对应工位的曲线面板</summary>
    public WorkPos WorkPos { get; init; }

    /// <summary>数据来源（RX/TX），UI 据此路由到对应类型的曲线面板</summary>
    public CouplingSource Source { get; init; }

    /// <summary>消息类型</summary>
    public CouplingSampleType Type { get; init; }

    /// <summary>位置（脉冲），仅 Sample 有效</summary>
    public int Position { get; init; }

    /// <summary>纵轴单位/含义（RSP 或 功率），面板据此设置 Y 轴标签，仅 Start 有效</summary>
    public string ValueLabel { get; init; } = string.Empty;

    /// <summary>通道号 → 数值（RX=ISP板RSP，TX=雅克贝斯控制器功率），按通道号升序取前 N 个，仅 Sample 有效</summary>
    public IReadOnlyDictionary<int, double> ChannelValues { get; init; } = new Dictionary<int, double>();

    /// <summary>批量采样点 X 轴位置序列（脉冲），仅 Batch 有效</summary>
    public IReadOnlyList<double> Positions { get; init; } = [];

    /// <summary>批量采样：通道号 → 数值序列（与 Positions 等长），仅 Batch 有效</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<double>> ChannelSeries { get; init; } = new Dictionary<int, IReadOnlyList<double>>();
}
