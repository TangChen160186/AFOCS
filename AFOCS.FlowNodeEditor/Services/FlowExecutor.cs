using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.ViewModels;

namespace AFOCS.FlowNodeEditor.Services
{
    /// <summary>
    /// 流程执行引擎 —— 支持三种节点类型：
    ///   1. 流程节点：有 Execution 端口，按 Execution 链顺序执行
    ///   2. 数据节点：无 Execution 端口，被动/按需计算，为流程节点提供数据
    ///   3. 入口节点：有 Exec 输出但无 Exec 输入，创建共享上下文，沿执行链传递
    /// </summary>
    public class FlowExecutor
    {
        private readonly INodeRegistry _registry;
        private readonly HashSet<Guid> _executing = []; // 循环检测

        public FlowExecutor(INodeRegistry registry)
        {
            _registry = registry;
        }

        public async Task<Dictionary<Guid, Dictionary<string, object?>>> ExecuteAsync(
            IReadOnlyList<NodeViewModel> nodes,
            IReadOnlyList<ConnectionViewModel> connections)
        {
            var results = new Dictionary<Guid, Dictionary<string, object?>>();
            var context = new Dictionary<string, object?>();

            // 1. 找到入口节点（有 Execution 输出但没有 Execution 输入）
            var entryNode = nodes.FirstOrDefault(n =>
                n.Outputs.Any(o => o.PortType == NodePortType.Execution) &&
                !n.Inputs.Any(i => i.PortType == NodePortType.Execution));

            if (entryNode != null)
            {
                // 入口节点的属性作为初始上下文
                foreach (var kv in entryNode.PropertyValues)
                    context[kv.Key] = kv.Value;

                // 执行入口节点，其输出也放入上下文
                await ExecuteNodeWithDeps(entryNode, nodes, connections, results, context);

                // 沿 Execution 链递归执行
                await FollowExecutionChain(entryNode, nodes, connections, results, context);
            }
            else
            {
                // 无入口节点 = 纯数据图：按拓扑排序执行所有数据节点
                var order = GetTopologicalOrder(nodes, connections);
                foreach (var nodeId in order)
                {
                    var node = nodes.First(n => n.InstanceId == nodeId);
                    await ExecuteNodeWithDeps(node, nodes, connections, results, context);
                }
            }

            System.Diagnostics.Debug.WriteLine(
                $"[FlowExecutor] 执行完成，共 {results.Count}/{nodes.Count} 个节点");
            return results;
        }

        /// <summary>
        /// 沿 Execution 端口链递归执行下游节点
        /// </summary>
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

                await ExecuteNodeWithDeps(nextNode, nodes, connections, results, context);
                await FollowExecutionChain(nextNode, nodes, connections, results, context);
            }
        }

        /// <summary>
        /// 执行单个节点，递归解析其所有数据输入依赖
        /// </summary>
        private async Task ExecuteNodeWithDeps(
            NodeViewModel node,
            IReadOnlyList<NodeViewModel> nodes,
            IReadOnlyList<ConnectionViewModel> connections,
            Dictionary<Guid, Dictionary<string, object?>> results,
            Dictionary<string, object?> context)
        {
            // 已执行过则跳过
            if (results.ContainsKey(node.InstanceId)) return;

            // 循环检测
            if (!_executing.Add(node.InstanceId))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FlowExecutor] 节点 '{node.Title}' 存在循环依赖，跳过");
                return;
            }

            try
            {
                // 收集输入值（递归解析数据依赖）
                var inputs = new Dictionary<string, object?>();
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
                            await ExecuteNodeWithDeps(sourceNode, nodes, connections, results, context);
                            if (results.TryGetValue(sourceNode.InstanceId, out var srcOutputs) &&
                                srcOutputs.TryGetValue(conn.Output.Name, out var val))
                            {
                                inputs[input.Name] = val;
                            }
                        }
                    }
                }

                // 执行节点
                var def = _registry.GetDefinition(node.Definition.TypeId);
                if (def is IExecutableNode execNode)
                {
                    var outputs = await execNode.ExecuteAsync(inputs, node.PropertyValues, context);
                    results[node.InstanceId] = outputs;

                    // 流程节点/入口节点的输出合并到上下文（供下游节点使用）
                    if (node.Outputs.Any(o => o.PortType == NodePortType.Execution))
                    {
                        foreach (var kv in outputs)
                            context[kv.Key] = kv.Value;
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[FlowExecutor] 节点 '{node.Title}' 执行成功，输出 {outputs.Count} 项");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[FlowExecutor] 节点 '{node.Title}' 未实现 IExecutableNode，跳过");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FlowExecutor] 节点 '{node.Title}' 执行失败: {ex.Message}");
                results[node.InstanceId] = new Dictionary<string, object?> { ["_error"] = ex.Message };
            }
            finally
            {
                _executing.Remove(node.InstanceId);
            }
        }

        /// <summary>
        /// 纯数据图的拓扑排序（所有连接参与）
        /// </summary>
        private static List<Guid> GetTopologicalOrder(
            IReadOnlyList<NodeViewModel> nodes,
            IReadOnlyList<ConnectionViewModel> connections)
        {
            var inDegree = nodes.ToDictionary(n => n.InstanceId, _ => 0);
            var adjacency = nodes.ToDictionary(n => n.InstanceId, _ => new List<Guid>());

            foreach (var conn in connections)
            {
                adjacency[conn.Output.ParentInstanceId].Add(conn.Input.ParentInstanceId);
                inDegree[conn.Input.ParentInstanceId]++;
            }

            var queue = new Queue<Guid>();
            foreach (var n in nodes)
                if (inDegree[n.InstanceId] == 0)
                    queue.Enqueue(n.InstanceId);

            var result = new List<Guid>();
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                result.Add(current);
                foreach (var neighbor in adjacency[current])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                        queue.Enqueue(neighbor);
                }
            }

            return result;
        }
    }
}
