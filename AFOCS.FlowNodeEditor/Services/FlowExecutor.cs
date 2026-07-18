using System.Reflection;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.ViewModels;

namespace AFOCS.FlowNodeEditor.Services
{
    /// <summary>
    /// 流程执行引擎。
    /// 输入 → 框架自动写入实例的 [NodeInput] 属性 → ExecuteAsync(context) → 框架自动读取实例的 [NodeOutput] 属性 → 输出
    /// </summary>
    public class FlowExecutor
    {
        private readonly HashSet<Guid> _executing = [];

        public async Task<Dictionary<Guid, Dictionary<string, object?>>> ExecuteAsync(
            IReadOnlyList<NodeViewModel> nodes,
            IReadOnlyList<ConnectionViewModel> connections)
        {
            var results = new Dictionary<Guid, Dictionary<string, object?>>();
            var context = new Dictionary<string, object?>();

            var entryNode = nodes.FirstOrDefault(n =>
                n.Outputs.Any(o => o.PortType == NodePortType.Execution) &&
                !n.Inputs.Any(i => i.PortType == NodePortType.Execution));

            if (entryNode != null)
            {
                await ExecuteNodeWithDeps(entryNode, nodes, connections, results, context);
                await FollowExecutionChain(entryNode, nodes, connections, results, context);
            }
            else
            {
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

        private async Task ExecuteNodeWithDeps(
            NodeViewModel node,
            IReadOnlyList<NodeViewModel> nodes,
            IReadOnlyList<ConnectionViewModel> connections,
            Dictionary<Guid, Dictionary<string, object?>> results,
            Dictionary<string, object?> context)
        {
            if (results.ContainsKey(node.InstanceId)) return;

            if (!_executing.Add(node.InstanceId))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FlowExecutor] 节点 '{node.Title}' 存在循环依赖，跳过");
                return;
            }

            try
            {
                var instance = node.Definition;
                var defType = instance.GetType();
                var inputPropMap = NodeDefinitionScanner.GetInputPropertyMap(defType);
                var outputPropMap = NodeDefinitionScanner.GetOutputPropertyMap(defType);

                // 1. 将连接的输入值写入实例属性
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
                                // 如果有关联的属性，写入实例属性（带类型转换）
                                if (inputPropMap.TryGetValue(input.Name, out var prop) && prop != null && prop.CanWrite)
                                    prop.SetValue(instance, ConvertValue(val, prop.PropertyType));
                            }
                        }
                    }
                }

                // 2. 执行节点
                if (instance is IExecutableNode execNode)
                {
                    await execNode.ExecuteAsync(context);

                    // 3. 从实例属性读取输出值
                    var outputs = new Dictionary<string, object?>();
                    foreach (var output in node.Outputs)
                    {
                        if (output.PortType == NodePortType.Execution)
                            continue;

                        if (outputPropMap.TryGetValue(output.Name, out var prop) && prop != null)
                            outputs[output.Name] = prop.GetValue(instance);
                    }

                    results[node.InstanceId] = outputs;

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

        /// <summary>将值安全转换为目标属性类型（如 string→int, int→double 等）</summary>
        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsInstanceOfType(value)) return value;

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            try
            {
                return Convert.ChangeType(value, underlyingType);
            }
            catch
            {
                return value;
            }
        }
    }
}
