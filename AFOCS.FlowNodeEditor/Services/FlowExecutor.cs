using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.ViewModels;
using AFOCS.Infrastructure;
using Caliburn.Micro;
using Serilog;

namespace AFOCS.FlowNodeEditor.Services;

public enum NodeExecutionState
{
    Idle,
    Executing,
    Completed,
    Error
}

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class FlowExecutor
{
    private readonly ILogger _logger;
    private readonly IEventAggregator _eventAggregator;

    /// <summary>分支结果在 ExecuteAsync 返回值中的约定 Key（bool 值，true=真分支，false=假分支）</summary>
    public const string BranchResultKey = "_branch";

    /// <summary>真分支执行输出端口名（与 IfNodeDefinition 的端口名一致）</summary>
    public const string TrueBranchPortName = "True";

    /// <summary>假分支执行输出端口名（与 IfNodeDefinition 的端口名一致）</summary>
    public const string FalseBranchPortName = "False";

    /// <summary>上下文中的工位 Key</summary>
    public const string WorkPosKey = "WorkPos";

    /// <summary>上下文中取消令牌的 Key（实现 ICancellableExecutableNode 的节点可从中读取）</summary>
    public const string CancellationTokenKey = "CancellationToken";

    /// <summary>已执行（或已禁用跳过）的节点集合</summary>
    private readonly ConcurrentDictionary<Guid, byte> _executed = [];

    private readonly ConcurrentDictionary<Guid, Stopwatch> _nodeTimers = [];

    /// <summary>
    /// 节点执行任务缓存（只含"执行自身"，不含下游传播）：
    /// 同一节点无论被多少条路径触发都只创建一个执行任务。
    /// 返回值表示该节点是否执行成功（决定是否向下游传播）。
    /// </summary>
    private readonly ConcurrentDictionary<Guid, Lazy<Task<bool>>> _executionTasks = [];

    /// <summary>
    /// 节点传播任务缓存（等执行完成后再并行扇出下游）。
    /// 执行与传播分离，避免"扇出等待下游 + 汇聚等待上游"形成循环等待/递归访问自身任务。
    /// </summary>
    private readonly ConcurrentDictionary<Guid, Lazy<Task>> _propagationTasks = [];

    private readonly ConcurrentDictionary<Guid, Dictionary<string, object?>> _results = [];

    /// <summary>全局共享上下文（节点签名固定为 Dictionary，故并发写入需加锁）</summary>
    private readonly Dictionary<string, object?> _context = [];
    private readonly object _contextLock = new();

    /// <summary>本次执行的取消源：任一并行节点失败时取消其余节点</summary>
    private CancellationTokenSource _cts = new();

    private WorkPos _currentWorkPos;

    [ImportingConstructor]
    public FlowExecutor(ILogger logger, IEventAggregator eventAggregator)
    {
        _logger = logger;
        _eventAggregator = eventAggregator;
    }

    /// <summary>节点状态变化回调（节点实例Id, 状态）</summary>
    public event Action<Guid, NodeExecutionState>? NodeStateChanged;

    /// <summary>设置当前执行的全局工位（在调用 ExecuteFromNodeAsync/ExecuteSingleNodeAsync 之前需要先设置）</summary>
    public void SetWorkPos(WorkPos workPos)
    {
        _currentWorkPos = workPos;
        _nodeTimers.Clear();

        // 通知订阅方：新一轮流程开始，可据此清空上一轮记录
        _ = _eventAggregator.PublishOnUIThreadAsync(new FlowExecutionStartedMessage { WorkPos = workPos });
    }

    public async Task<Dictionary<Guid, Dictionary<string, object?>>> ExecuteAsync(
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections,
        WorkPos workPos)
    {
        SetWorkPos(workPos);

        var entryNodes = nodes.Where(n =>
            n.Outputs.Any(o => o.PortType == NodePortType.Execution) &&
            n.Inputs.All(i => i.PortType != NodePortType.Execution)).ToList();

        if (entryNodes.Count == 0)
        {
            _logger.Information("[FlowExecutor] 未找到 Entry 节点，无法执行");
            return new Dictionary<Guid, Dictionary<string, object?>>();
        }

        if (entryNodes.Count == 1)
        {
            return await ExecuteFromNodeAsync(entryNodes[0], nodes, connections);
        }

        return await ExecuteWithPriorityAsync(entryNodes, nodes, connections);
    }

    private async Task<Dictionary<Guid, Dictionary<string, object?>>> ExecuteWithPriorityAsync(
        List<NodeViewModel> entryNodes,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections)
    {
        ResetExecutionState();
        _context[WorkPosKey] = _currentWorkPos;

        var grouped = entryNodes.GroupBy(n =>
            {
                var priorityProp = n.Definition.GetType().GetProperty("Priority");
                return priorityProp != null ? (int)priorityProp.GetValue(n.Definition)! : 0;
            })
            .OrderBy(g => g.Key)
            .ToList();

        _logger.Information($"[FlowExecutor] 找到 {entryNodes.Count} 个入口，按优先级分组: {string.Join(", ", grouped.Select(g => $"优先级{g.Key}({g.Count()}个)"))}");

        foreach (var group in grouped)
        {
            _logger.Information($"[FlowExecutor] 执行优先级 {group.Key} 的 {group.Count()} 个入口(并行)");

            var tasks = group.Select(entry => GetPropagationTask(entry, nodes, connections, isEntry: true));
            await Task.WhenAll(tasks);
        }

        _logger.Information(
            $"[FlowExecutor] 执行完成，共 {_executed.Count}/{nodes.Count} 个节点");
        return new Dictionary<Guid, Dictionary<string, object?>>(_results);
    }

    public async Task<Dictionary<Guid, Dictionary<string, object?>>> ExecuteFromNodeAsync(
        NodeViewModel startNode,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections)
    {
        ResetExecutionState();
        _context[WorkPosKey] = _currentWorkPos;

        _logger.Information($"[FlowExecutor] 从节点 '{startNode.Title}' 开始执行");

        // 入口的传播任务 = 整条流程：等自身执行完成后向下游逐级传播
        await GetPropagationTask(startNode, nodes, connections, isEntry: true);

        _logger.Information(
            $"[FlowExecutor] 执行完成，共 {_executed.Count}/{nodes.Count} 个节点");
        return new Dictionary<Guid, Dictionary<string, object?>>(_results);
    }

    public async Task<Dictionary<Guid, Dictionary<string, object?>>> ExecuteSingleNodeAsync(
        NodeViewModel node,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections)
    {
        ResetExecutionState();
        _context[WorkPosKey] = _currentWorkPos;

        _logger.Information($"[FlowExecutor] 只执行节点 '{node.Title}'");

        foreach (var input in node.Inputs)
        {
            if (input.PortType == NodePortType.Execution)
                continue;

            var conn = connections.FirstOrDefault(c => c.Input == input);
            if (conn != null)
            {
                var sourceNode = nodes.FirstOrDefault(n => n.InstanceId == conn.Output.ParentInstanceId);
                if (sourceNode != null)
                {
                    var sourceProp = GetPropertyByPortName(sourceNode.Definition, conn.Output.Name);
                    if (sourceProp != null)
                    {
                        var val = sourceProp.GetValue(sourceNode.Definition);
                        SetInputPortValue(node.Definition, input.Name, val);
                    }
                }
            }
        }

        var isEnabled = node.Definition.GetType()
            .GetProperty("Enabled")?.GetValue(node.Definition) as bool? ?? true;

        if (!isEnabled)
        {
            _logger.Information(
                $"[FlowExecutor] 节点 '{node.Title}' 已禁用，跳过执行");
            _executed[node.InstanceId] = 0;
            return new Dictionary<Guid, Dictionary<string, object?>>();
        }

        await ExecuteNodeInternalAsync(node);
        return new Dictionary<Guid, Dictionary<string, object?>>(_results);
    }

    // ========== 核心执行（执行/传播分离） ==========

    private void ResetExecutionState()
    {
        _executed.Clear();
        _nodeTimers.Clear();
        _executionTasks.Clear();
        _propagationTasks.Clear();
        _results.Clear();
        _context.Clear();

        // 每次执行使用全新的取消源，并把令牌注入上下文供可取消节点读取
        _cts = new CancellationTokenSource();
        _context[CancellationTokenKey] = _cts.Token;
    }

    /// <summary>
    /// 获取节点执行任务（幂等）：同一节点在扇出/汇聚场景下可能被多条路径同时触发，
    /// 通过 Lazy 保证只创建一个执行任务，其余路径等待同一任务。
    /// </summary>
    private Task<bool> GetExecutionTask(
        NodeViewModel node,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections,
        bool isEntry)
    {
        return _executionTasks.GetOrAdd(
            node.InstanceId,
            _ => new Lazy<Task<bool>>(() => ExecuteNodeSelfAsync(node, nodes, connections, isEntry),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    /// <summary>
    /// 执行节点自身（不向下游传播）：
    /// 1. 汇聚屏障：等待所有执行输入来源节点完成（多个上游并行执行完后才执行本节点）；
    /// 2. 数据依赖：先执行数据输入来源节点，再将其输出注入本节点输入；
    /// 3. 执行本节点。
    /// 返回是否执行成功（决定传播任务是否继续扇出下游）。
    /// </summary>
    private async Task<bool> ExecuteNodeSelfAsync(
        NodeViewModel node,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections,
        bool isEntry)
    {
        // 1. 汇聚：等待所有执行输入来源完成（入口节点无执行输入，跳过）
        if (!isEntry)
        {
            var execSources = GetExecutionSourceNodes(node, connections, nodes);
            if (execSources.Count > 0)
                await Task.WhenAll(execSources.Select(s => GetExecutionTask(s, nodes, connections, false)));
        }

        // 2. 数据依赖：先执行来源节点，再传递值
        foreach (var input in node.Inputs)
        {
            if (input.PortType == NodePortType.Execution)
                continue;

            var conn = connections.FirstOrDefault(c => c.Input == input);
            if (conn == null) continue;

            var sourceNode = nodes.FirstOrDefault(n => n.InstanceId == conn.Output.ParentInstanceId);
            if (sourceNode == null) continue;

            await GetExecutionTask(sourceNode, nodes, connections, false);

            if (_results.TryGetValue(sourceNode.InstanceId, out var srcOutputs) &&
                srcOutputs.TryGetValue(conn.Output.Name, out var val))
            {
                SetInputPortValue(node.Definition, input.Name, val);
            }
        }

        // 3. 禁用节点：不执行也不向下游传播
        var isEnabled = node.Definition.GetType()
            .GetProperty("Enabled")?.GetValue(node.Definition) as bool? ?? true;

        if (!isEnabled)
        {
            _logger.Information(
                $"[FlowExecutor] 节点 '{node.Title}' 已禁用，跳过执行");
            _executed[node.InstanceId] = 0;
            return false;
        }

        // 4. 执行本节点
        return await ExecuteNodeInternalAsync(node);
    }

    /// <summary>
    /// 获取节点传播任务（幂等）：执行完成后并行扇出到所有下游节点。
    /// </summary>
    private Task GetPropagationTask(
        NodeViewModel node,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections,
        bool isEntry)
    {
        return _propagationTasks.GetOrAdd(
            node.InstanceId,
            _ => new Lazy<Task>(() => PropagateAsync(node, nodes, connections, isEntry),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private async Task PropagateAsync(
        NodeViewModel node,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections,
        bool isEntry)
    {
        var shouldPropagate = await GetExecutionTask(node, nodes, connections, isEntry);
        if (!shouldPropagate) return;

        // 扇出：并行执行所有下游节点的传播任务（条件分支节点仅跟随所选分支）
        var downstream = GetDownstreamNodes(node, connections, nodes);
        if (downstream.Count > 0)
            await Task.WhenAll(downstream.Select(d => GetPropagationTask(d, nodes, connections, false)));
    }

    /// <summary>获取节点所有执行输入端口连接的来源节点（用于汇聚等待）</summary>
    private static List<NodeViewModel> GetExecutionSourceNodes(
        NodeViewModel node,
        IReadOnlyList<ConnectionViewModel> connections,
        IReadOnlyList<NodeViewModel> nodes)
    {
        var result = new List<NodeViewModel>();
        foreach (var input in node.Inputs.Where(i => i.PortType == NodePortType.Execution))
        {
            foreach (var conn in connections.Where(c => c.Input == input))
            {
                var src = nodes.FirstOrDefault(n => n.InstanceId == conn.Output.ParentInstanceId);
                if (src != null && !result.Contains(src))
                    result.Add(src);
            }
        }
        return result;
    }

    /// <summary>
    /// 获取节点执行输出连接的下游节点。
    /// 多执行输出端口（条件分支节点）：按执行结果 _branch 只选择 True/False 分支；
    /// 普通节点：扇出到所有执行输出连接的下游。
    /// </summary>
    private List<NodeViewModel> GetDownstreamNodes(
        NodeViewModel node,
        IReadOnlyList<ConnectionViewModel> connections,
        IReadOnlyList<NodeViewModel> nodes)
    {
        var execOutputs = node.Outputs.Where(o => o.PortType == NodePortType.Execution).ToList();
        if (execOutputs.Count == 0) return [];

        // 多执行输出端口（条件分支节点）：根据执行结果 _branch 选择要跟随的分支
        if (execOutputs.Count > 1)
        {
            var branch = _results.TryGetValue(node.InstanceId, out var outputs)
                         && outputs.TryGetValue(BranchResultKey, out var b)
                ? b as bool?
                : null;

            if (branch is bool taken)
            {
                var branchOutput = execOutputs.FirstOrDefault(o =>
                    o.Name == (taken ? TrueBranchPortName : FalseBranchPortName));

                if (branchOutput != null)
                    return GetNodesConnectedToOutput(branchOutput, connections, nodes);

                _logger.Information(
                    $"[FlowExecutor] 节点 '{node.Title}' 分支结果 {taken}，但未找到对应输出端口");
                return [];
            }

            _logger.Warning(
                $"[FlowExecutor] 节点 '{node.Title}' 有多个执行输出但缺少分支结果，按所有端口执行");
        }

        // 扇出：收集所有执行输出连接的下游节点（去重）
        var downstream = new List<NodeViewModel>();
        foreach (var output in execOutputs)
        {
            foreach (var nextNode in GetNodesConnectedToOutput(output, connections, nodes))
            {
                if (!downstream.Contains(nextNode))
                    downstream.Add(nextNode);
            }
        }
        return downstream;
    }

    private static List<NodeViewModel> GetNodesConnectedToOutput(
        ConnectorViewModel execOutput,
        IReadOnlyList<ConnectionViewModel> connections,
        IReadOnlyList<NodeViewModel> nodes)
    {
        return connections.Where(c => c.Output == execOutput)
            .Select(c => nodes.FirstOrDefault(n => n.Inputs.Contains(c.Input)))
            .Where(n => n != null)
            .Cast<NodeViewModel>()
            .ToList();
    }

    private static PropertyInfo? GetPropertyByPortName(INodeDefinition definition, string portName)
    {
        return definition.GetType().GetProperties()
            .FirstOrDefault(p => p.Name == portName ||
                                 p.GetCustomAttribute<NodePortAttribute>()?.Name == portName);
    }

    /// <summary>
    /// 执行单个节点并记录计时与发布消息。
    /// 返回是否执行成功（决定传播任务是否继续扇出下游）。
    /// </summary>
    private async Task<bool> ExecuteNodeInternalAsync(NodeViewModel node)
    {
        if (_executed.ContainsKey(node.InstanceId))
            return false;

        var sw = Stopwatch.StartNew();
        _nodeTimers[node.InstanceId] = sw;

        var ct = _cts.Token;

        NodeStateChanged?.Invoke(node.InstanceId, NodeExecutionState.Executing);
        node.IsExecuting = true;
        node.IsCompleted = false;

        try
        {
            // 执行前检查：其它并行分支出错触发取消后，尚未开始执行的节点直接标记取消
            if (ct.IsCancellationRequested)
                throw new OperationCanceledException(ct);

            if (node.Definition is IExecutableNode execNode)
            {
                var outputs = node.Definition is ICancellableExecutableNode cancelNode
                    ? await cancelNode.ExecuteAsync(_context, ct)
                    : await execNode.ExecuteAsync(_context);
                sw.Stop();

                _results[node.InstanceId] = outputs;
                _executed[node.InstanceId] = 0;

                // 并行执行时多个节点可能同时写共享 context，需加锁
                lock (_contextLock)
                {
                    foreach (var kv in outputs)
                        _context[kv.Key] = kv.Value;
                }

                _logger.Information(
                    $"[FlowExecutor] 节点 '{node.Title}' 执行成功，输出 {outputs.Count} 项，耗时 {sw.ElapsedMilliseconds}ms");

                node.IsExecuting = false;
                node.IsCompleted = true;
                NodeStateChanged?.Invoke(node.InstanceId, NodeExecutionState.Completed);

                PublishMessage(node, isSuccess: true, elapsedMs: sw.ElapsedMilliseconds);
                return true;
            }
            else
            {
                sw.Stop();
                _logger.Information(
                    $"[FlowExecutor] 节点 '{node.Title}' 未实现 IExecutableNode，跳过");
                node.IsExecuting = false;
                return false;
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // 节点被取消（其它并行分支出错）：仅标记状态，不抛出，避免掩盖原始错误
            sw.Stop();
            _logger.Information($"[FlowExecutor] 节点 '{node.Title}' 已取消，耗时 {sw.ElapsedMilliseconds}ms");
            _results[node.InstanceId] = new Dictionary<string, object?> { ["_error"] = "执行已取消" };
            _executed[node.InstanceId] = 0;

            node.IsExecuting = false;
            node.HasError = true;
            NodeStateChanged?.Invoke(node.InstanceId, NodeExecutionState.Error);
            return false;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.Information(
                $"[FlowExecutor] 节点 '{node.Title}' 执行失败: {ex.Message}，耗时 {sw.ElapsedMilliseconds}ms");
            _results[node.InstanceId] = new Dictionary<string, object?> { ["_error"] = ex.Message };
            _executed[node.InstanceId] = 0;

            // 节点出错：取消其它并行分支的执行
            _cts.Cancel();

            node.IsExecuting = false;
            node.HasError = true;
            NodeStateChanged?.Invoke(node.InstanceId, NodeExecutionState.Error);

            PublishMessage(node, isSuccess: false, elapsedMs: sw.ElapsedMilliseconds, errorMessage: ex.Message);

            // 出错立即终止流程，不再执行任何下游节点
            throw;
        }
    }

    /// <summary>通过 IEventAggregator 发布节点执行消息</summary>
    private void PublishMessage(NodeViewModel node, bool isSuccess, long elapsedMs, string? errorMessage = null)
    {
        var msg = new NodeExecutionMessage
        {
            WorkPos = _currentWorkPos,
            NodeTitle = node.Title,
            NodeDescription = node.Definition.GetType()
                .GetProperty("Description")?.GetValue(node.Definition) as string ?? string.Empty,
            NodeTypeId = NodeDefinitionHelper.GetTypeId(node.Definition) ?? "Unknown",
            IsSuccess = isSuccess,
            ElapsedMs = elapsedMs,
            ErrorMessage = errorMessage,
        };

        _eventAggregator.PublishOnUIThreadAsync(msg);
    }

    private static void SetInputPortValue(INodeDefinition definition, string portName, object? value)
    {
        var type = definition.GetType();
        var prop = type.GetProperty(portName, BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(definition, value);
            return;
        }
        var field = type.GetField(portName, BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(definition, value);
        }
    }
}
