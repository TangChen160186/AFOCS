using System.Reflection;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.ViewModels;

namespace AFOCS.FlowNodeEditor.Services
{
    public enum NodeExecutionState
    {
        Idle,
        Executing,
        Completed,
        Error
    }

    public class FlowExecutor
    {
        private readonly INodeRegistry _registry;
        private readonly HashSet<Guid> _executed = [];

        /// <summary>节点状态变化回调（节点实例Id, 状态）</summary>
        public event Action<Guid, NodeExecutionState>? NodeStateChanged;

        public FlowExecutor(INodeRegistry registry)
        {
            _registry = registry;
        }

        public async Task<Dictionary<Guid, Dictionary<string, object?>>> ExecuteAsync(
            IReadOnlyList<NodeViewModel> nodes,
            IReadOnlyList<ConnectionViewModel> connections)
        {
            var entryNodes = nodes.Where(n =>
                n.Outputs.Any(o => o.PortType == NodePortType.Execution) &&
                !n.Inputs.Any(i => i.PortType == NodePortType.Execution)).ToList();

            if (entryNodes.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[FlowExecutor] 未找到 Entry 节点，无法执行");
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
            var context = new Dictionary<string, object?>();

            var grouped = entryNodes.GroupBy(n =>
            {
                var priorityProp = n.Definition.GetType().GetProperty("Priority");
                return priorityProp != null ? (int)priorityProp.GetValue(n.Definition)! : 0;
            })
            .OrderBy(g => g.Key)
            .ToList();

            System.Diagnostics.Debug.WriteLine($"[FlowExecutor] 找到 {entryNodes.Count} 个入口，按优先级分组: {string.Join(", ", grouped.Select(g => $"优先级{g.Key}({g.Count()}个)"))}");

            foreach (var group in grouped)
            {
                System.Diagnostics.Debug.WriteLine($"[FlowExecutor] 执行优先级 {group.Key} 的 {group.Count()} 个入口(并行)");

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

            System.Diagnostics.Debug.WriteLine(
                $"[FlowExecutor] 执行完成，共 {results.Count}/{nodes.Count} 个节点");
            return results;
        }

        public async Task<Dictionary<Guid, Dictionary<string, object?>>> ExecuteFromNodeAsync(
            NodeViewModel startNode,
            IReadOnlyList<NodeViewModel> nodes,
            IReadOnlyList<ConnectionViewModel> connections)
        {
            var results = new Dictionary<Guid, Dictionary<string, object?>>();
            var context = new Dictionary<string, object?>();
            _executed.Clear();

            System.Diagnostics.Debug.WriteLine($"[FlowExecutor] 从节点 '{startNode.Title}' 开始执行");

            await ExecuteNodeWithDeps(startNode, nodes, connections, results, context);
            await FollowExecutionChain(startNode, nodes, connections, results, context);

            System.Diagnostics.Debug.WriteLine(
                $"[FlowExecutor] 执行完成，共 {_executed.Count}/{nodes.Count} 个节点");
            return results;
        }

        public async Task<Dictionary<Guid, Dictionary<string, object?>>> ExecuteSingleNodeAsync(
            NodeViewModel node,
            IReadOnlyList<NodeViewModel> nodes,
            IReadOnlyList<ConnectionViewModel> connections)
        {
            var results = new Dictionary<Guid, Dictionary<string, object?>>();
            var context = new Dictionary<string, object?>();
            _executed.Clear();

            System.Diagnostics.Debug.WriteLine($"[FlowExecutor] 只执行节点 '{node.Title}'");

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
                System.Diagnostics.Debug.WriteLine(
                    $"[FlowExecutor] 节点 '{node.Title}' 已禁用，跳过执行");
                return results;
            }

            NodeStateChanged?.Invoke(node.InstanceId, NodeExecutionState.Executing);
            node.IsExecuting = true;
            node.IsCompleted = false;

            try
            {
                if (node.Definition is IExecutableNode execNode)
                {
                    var outputs = await execNode.ExecuteAsync(context);
                    results[node.InstanceId] = outputs;

                    foreach (var kv in outputs)
                    {
                        var prop = node.Definition.GetType().GetProperty(kv.Key);
                        if (prop != null && prop.CanWrite)
                            prop.SetValue(node.Definition, kv.Value);
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[FlowExecutor] 节点 '{node.Title}' 执行成功，输出 {outputs.Count} 项");

                    node.IsExecuting = false;
                    node.IsCompleted = true;
                    NodeStateChanged?.Invoke(node.InstanceId, NodeExecutionState.Completed);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[FlowExecutor] 节点 '{node.Title}' 未实现 IExecutableNode，跳过");
                    node.IsExecuting = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FlowExecutor] 节点 '{node.Title}' 执行失败: {ex.Message}");
                results[node.InstanceId] = new Dictionary<string, object?> { ["_error"] = ex.Message };

                node.IsExecuting = false;
                node.HasError = true;
                NodeStateChanged?.Invoke(node.InstanceId, NodeExecutionState.Error);
            }

            return results;
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
            var execOutput = fromNode.Outputs.FirstOrDefault(o => o.PortType == NodePortType.Execution);
            if (execOutput == null) return;

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
                System.Diagnostics.Debug.WriteLine(
                    $"[FlowExecutor] 节点 '{node.Title}' 已禁用，跳过执行");
                _executed.Add(node.InstanceId);
                return;
            }

            NodeStateChanged?.Invoke(node.InstanceId, NodeExecutionState.Executing);
            node.IsExecuting = true;
            node.IsCompleted = false;

            try
            {
                if (node.Definition is IExecutableNode execNode)
                {
                    var outputs = await execNode.ExecuteAsync(context);
                    results[node.InstanceId] = outputs;
                    _executed.Add(node.InstanceId);

                    foreach (var kv in outputs)
                        context[kv.Key] = kv.Value;

                    System.Diagnostics.Debug.WriteLine(
                        $"[FlowExecutor] 节点 '{node.Title}' 执行成功，输出 {outputs.Count} 项");

                    node.IsExecuting = false;
                    node.IsCompleted = true;
                    NodeStateChanged?.Invoke(node.InstanceId, NodeExecutionState.Completed);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[FlowExecutor] 节点 '{node.Title}' 未实现 IExecutableNode，跳过");
                    node.IsExecuting = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FlowExecutor] 节点 '{node.Title}' 执行失败: {ex.Message}");
                results[node.InstanceId] = new Dictionary<string, object?> { ["_error"] = ex.Message };
                _executed.Add(node.InstanceId);

                node.IsExecuting = false;
                node.HasError = true;
                NodeStateChanged?.Invoke(node.InstanceId, NodeExecutionState.Error);
            }
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
}