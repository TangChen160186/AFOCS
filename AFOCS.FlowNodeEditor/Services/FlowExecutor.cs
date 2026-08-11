using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Reflection;
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

    private readonly HashSet<Guid> _executed = [];
    private WorkPos _currentWorkPos;
    private readonly Dictionary<Guid, Stopwatch> _nodeTimers = [];

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
    }

    public async Task<Dictionary<Guid, Dictionary<string, object?>>> ExecuteAsync(
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections,
        WorkPos workPos)
    {
        _currentWorkPos = workPos;
        _nodeTimers.Clear();

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
        var results = new Dictionary<Guid, Dictionary<string, object?>>();
        var context = new Dictionary<string, object?> { [WorkPosKey] = _currentWorkPos };

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

            var tasks = group.Select(async entryNode =>
            {
                var localResults = await ExecuteFromNodeAsync(entryNode, nodes, connections);
                lock (results)
                {
                    foreach (var kv in localResults)
                    {
                        if (!results.ContainsKey(kv.Key))
                            results[kv.Key] = kv.Value;
                    }
                }
                lock (context)
                {
                    foreach (var kv in localResults)
                    {
                        foreach (var outputKv in kv.Value)
                        {
                            context[outputKv.Key] = outputKv.Value;
                        }
                    }
                }
            });

            await Task.WhenAll(tasks);
        }

        _logger.Information(
            $"[FlowExecutor] 执行完成，共 {results.Count}/{nodes.Count} 个节点");
        return results;
    }

    public async Task<Dictionary<Guid, Dictionary<string, object?>>> ExecuteFromNodeAsync(
        NodeViewModel startNode,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections)
    {
        var results = new Dictionary<Guid, Dictionary<string, object?>>();
        var context = new Dictionary<string, object?> { [WorkPosKey] = _currentWorkPos };
        _executed.Clear();

        _logger.Information($"[FlowExecutor] 从节点 '{startNode.Title}' 开始执行");

        await ExecuteNodeWithDeps(startNode, nodes, connections, results, context);
        await FollowExecutionChain(startNode, nodes, connections, results, context);

        _logger.Information(
            $"[FlowExecutor] 执行完成，共 {_executed.Count}/{nodes.Count} 个节点");
        return results;
    }

    public async Task<Dictionary<Guid, Dictionary<string, object?>>> ExecuteSingleNodeAsync(
        NodeViewModel node,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections)
    {
        var results = new Dictionary<Guid, Dictionary<string, object?>>();
        var context = new Dictionary<string, object?> { [WorkPosKey] = _currentWorkPos };
        _executed.Clear();

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
            return results;
        }

        return await ExecuteNodeInternalAsync(node, context, results);
    }

    private static PropertyInfo? GetPropertyByPortName(INodeDefinition definition, string portName)
    {
        return definition.GetType().GetProperties()
            .FirstOrDefault(p => p.Name == portName ||
                                 p.GetCustomAttribute<NodePortAttribute>()?.Name == portName);
    }

    private async Task FollowExecutionChain(
        NodeViewModel fromNode,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections,
        Dictionary<Guid, Dictionary<string, object?>> results,
        Dictionary<string, object?> context)
    {
        // 如果当前节点没有产生执行结果（被禁用/跳过），终止链路传播
        if (!results.ContainsKey(fromNode.InstanceId))
        {
            _logger.Information($"[FlowExecutor] 节点 '{fromNode.Title}' 未执行（已禁用），停止向下游传播");
            return;
        }

        var execOutputs = fromNode.Outputs.Where(o => o.PortType == NodePortType.Execution).ToList();
        if (execOutputs.Count == 0) return;

        // 多执行输出端口（条件分支节点）：根据执行结果 _branch 选择要跟随的分支
        if (execOutputs.Count > 1)
        {
            var branch = results.TryGetValue(fromNode.InstanceId, out var outputs)
                         && outputs.TryGetValue(BranchResultKey, out var b)
                ? b as bool?
                : null;

            if (branch is bool taken)
            {
                var branchOutput = execOutputs.FirstOrDefault(o =>
                    o.Name == (taken ? TrueBranchPortName : FalseBranchPortName));

                if (branchOutput != null)
                {
                    await FollowExecutionOutput(branchOutput, nodes, connections, results, context);
                    return;
                }

                _logger.Information(
                    $"[FlowExecutor] 节点 '{fromNode.Title}' 分支结果 {taken}，但未找到对应输出端口");
                return;
            }

            _logger.Warning(
                $"[FlowExecutor] 节点 '{fromNode.Title}' 有多个执行输出但缺少分支结果，按第一个端口执行");
        }

        await FollowExecutionOutput(execOutputs[0], nodes, connections, results, context);
    }

    private async Task FollowExecutionOutput(
        ConnectorViewModel execOutput,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections,
        Dictionary<Guid, Dictionary<string, object?>> results,
        Dictionary<string, object?> context)
    {
        var nextConns = connections.Where(c => c.Output == execOutput).ToList();
        foreach (var conn in nextConns)
        {
            var nextNode = nodes.FirstOrDefault(n => n.Inputs.Contains(conn.Input));
            if (nextNode == null) continue;
            if (_executed.Contains(nextNode.InstanceId)) continue;

            await ExecuteNodeWithDeps(nextNode, nodes, connections, results, context);
            await FollowExecutionChain(nextNode, nodes, connections, results, context);
        }
    }

    private async Task ExecuteNodeWithDeps(
        NodeViewModel node,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections,
        Dictionary<Guid, Dictionary<string, object?>> results,
        Dictionary<string, object?> context)
    {
        if (_executed.Contains(node.InstanceId)) return;

        // 先执行数据依赖的源节点
        foreach (var input in node.Inputs)
        {
            if (input.PortType == NodePortType.Execution)
                continue;

            var conn = connections.FirstOrDefault(c => c.Input == input);
            if (conn != null)
            {
                var sourceNode = nodes.FirstOrDefault(n => n.InstanceId == conn.Output.ParentInstanceId);
                if (sourceNode != null && !_executed.Contains(sourceNode.InstanceId))
                {
                    await ExecuteNodeWithDeps(sourceNode, nodes, connections, results, context);
                }

                // 传递数据
                if (results.TryGetValue(sourceNode!.InstanceId, out var srcOutputs) &&
                    srcOutputs.TryGetValue(conn.Output.Name, out var val))
                {
                    SetInputPortValue(node.Definition, input.Name, val);
                }
            }
        }

        var isEnabled = node.Definition.GetType()
            .GetProperty("Enabled")?.GetValue(node.Definition) as bool? ?? true;

        if (!isEnabled)
        {
            _logger.Information(
                $"[FlowExecutor] 节点 '{node.Title}' 已禁用，跳过执行");
            _executed.Add(node.InstanceId);
            return;
        }

        await ExecuteNodeInternalAsync(node, context, results);
    }

    /// <summary>执行单个节点并记录计时与发布消息</summary>
    private async Task<Dictionary<Guid, Dictionary<string, object?>>> ExecuteNodeInternalAsync(
        NodeViewModel node,
        Dictionary<string, object?> context,
        Dictionary<Guid, Dictionary<string, object?>> results)
    {
        if (_executed.Contains(node.InstanceId))
            return results;

        var sw = Stopwatch.StartNew();
        _nodeTimers[node.InstanceId] = sw;

        NodeStateChanged?.Invoke(node.InstanceId, NodeExecutionState.Executing);
        node.IsExecuting = true;
        node.IsCompleted = false;

        try
        {
            if (node.Definition is IExecutableNode execNode)
            {
                var outputs = await execNode.ExecuteAsync(context);
                sw.Stop();

                results[node.InstanceId] = outputs;
                _executed.Add(node.InstanceId);

                foreach (var kv in outputs)
                    context[kv.Key] = kv.Value;

                _logger.Information(
                    $"[FlowExecutor] 节点 '{node.Title}' 执行成功，输出 {outputs.Count} 项，耗时 {sw.ElapsedMilliseconds}ms");

                node.IsExecuting = false;
                node.IsCompleted = true;
                NodeStateChanged?.Invoke(node.InstanceId, NodeExecutionState.Completed);

                PublishMessage(node, isSuccess: true, elapsedMs: sw.ElapsedMilliseconds);
            }
            else
            {
                sw.Stop();
                _logger.Information(
                    $"[FlowExecutor] 节点 '{node.Title}' 未实现 IExecutableNode，跳过");
                node.IsExecuting = false;
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.Information(
                $"[FlowExecutor] 节点 '{node.Title}' 执行失败: {ex.Message}，耗时 {sw.ElapsedMilliseconds}ms");
            results[node.InstanceId] = new Dictionary<string, object?> { ["_error"] = ex.Message };
            _executed.Add(node.InstanceId);

            node.IsExecuting = false;
            node.HasError = true;
            NodeStateChanged?.Invoke(node.InstanceId, NodeExecutionState.Error);

            PublishMessage(node, isSuccess: false, elapsedMs: sw.ElapsedMilliseconds, errorMessage: ex.Message);

            // 出错立即终止流程，不再执行任何下游节点
            throw;
        }

        return results;
    }

    /// <summary>通过 IEventAggregator 发布节点执行消息</summary>
    private void PublishMessage(NodeViewModel node, bool isSuccess, long elapsedMs, string? errorMessage = null)
    {
        var msg = new NodeExecutionMessage
        {
            WorkPos = _currentWorkPos,
            NodeTitle = node.Title,
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
