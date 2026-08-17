namespace AFOCS.FlowNodeEditor.Services;

/// <summary>
/// 支持取消的可执行节点：当流程中其它并行节点执行失败时，FlowExecutor 会触发取消，
/// 实现本接口的节点应尽快中止自身操作（如将延时/等待改为可取消）。
/// 可选实现，普通节点（仅实现 IExecutableNode）不受影响，会照常执行完毕。
/// </summary>
public interface ICancellableExecutableNode : IExecutableNode
{
    Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context, CancellationToken cancellationToken);
}
