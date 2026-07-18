using System.Reflection;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.ViewModels;

namespace AFOCS.FlowNodeEditor.Services
{
    public class FlowExecutor
    {
        private readonly INodeRegistry _registry;
        private readonly HashSet<Guid> _executing = [];

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

            var entryNode = nodes.FirstOrDefault(n =>
                n.Outputs.Any(o => o.PortType == NodePortType.Execution) &&
                !n.Inputs.Any(i => i.PortType == NodePortType.Execution));

            if (entryNode != null)
            {
                foreach (var kv in GetNodeProperties(entryNode))
                    context[kv.Key] = kv.Value;

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

        private static Dictionary<string, object?> GetNodeProperties(NodeViewModel node)
        {
            var properties = new Dictionary<string, object?>();
            var type = node.Definition.GetType();
            
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (!NodeDefinitionHelper.AllowPropertyEdit(node.Definition, field.Name))
                    continue;
                properties[field.Name] = field.GetValue(node.Definition);
            }
            
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
                if (!NodeDefinitionHelper.AllowPropertyEdit(node.Definition, prop.Name))
                    continue;
                properties[prop.Name] = prop.GetValue(node.Definition);
            }
            
            return properties;
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
                                SetInputPortValue(node.Definition, input.Name, val);
                            }
                        }
                    }
                }

                var def = _registry.GetDefinition(NodeDefinitionHelper.GetTypeId(node.Definition));
                if (def is IExecutableNode execNode)
                {
                    var outputs = await execNode.ExecuteAsync(context);
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