using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using System.Text.Json;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.ViewModels;

namespace AFOCS.FlowNodeEditor.Services
{
    public interface IFlowExecutionService
    {
        Task<FlowExecutionResult> ExecuteFlowAsync(string filePath);
        Task<FlowExecutionResult> ExecuteFlowAsync(FlowGraph graph);
        Task<FlowExecutionResult> ExecuteFromNodeAsync(string filePath, Guid nodeInstanceId);
        Task<FlowExecutionResult> ExecuteSingleNodeAsync(string filePath, Guid nodeInstanceId);

        Task<Dictionary<string, FlowExecutionResult>> ExecuteFlowsAsync(IEnumerable<string> filePaths);
        Task<Dictionary<string, FlowExecutionResult>> ExecuteFlowsAsync(Dictionary<string, FlowGraph> graphs);
    }

    public class FlowExecutionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int ExecutedNodeCount { get; set; }
        public Dictionary<Guid, Dictionary<string, object?>> NodeOutputs { get; set; } = [];
        public List<string> ExecutionLog { get; set; } = [];
    }

    [Export(typeof(IFlowExecutionService))]
    public class FlowExecutionService : IFlowExecutionService
    {
        private readonly INodeRegistry _nodeRegistry;

        [ImportingConstructor]
        public FlowExecutionService(INodeRegistry nodeRegistry)
        {
            _nodeRegistry = nodeRegistry;
        }

        public async Task<FlowExecutionResult> ExecuteFlowAsync(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var graph = JsonSerializer.Deserialize<FlowGraph>(json);
                if (graph == null)
                {
                    return new FlowExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "无法解析流程文件"
                    };
                }

                return await ExecuteFlowAsync(graph);
            }
            catch (FileNotFoundException)
            {
                return new FlowExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"流程文件不存在: {filePath}"
                };
            }
            catch (Exception ex)
            {
                return new FlowExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"加载流程失败: {ex.Message}"
                };
            }
        }

        public async Task<FlowExecutionResult> ExecuteFlowAsync(FlowGraph graph)
        {
            var result = new FlowExecutionResult();

            try
            {
                var (nodes, connections) = await LoadFlowGraph(graph);
                result.ExecutionLog.Add($"加载流程: {nodes.Count} 个节点, {connections.Count} 条连线");

                var executor = new FlowExecutor(_nodeRegistry);
                executor.NodeStateChanged += (nodeId, state) =>
                {
                    var node = nodes.FirstOrDefault(n => n.InstanceId == nodeId);
                    if (node != null)
                    {
                        result.ExecutionLog.Add($"节点 '{node.Title}' 状态: {state}");
                    }
                };

                var outputs = await executor.ExecuteAsync(nodes.ToList(), connections.ToList());

                result.Success = true;
                result.ExecutedNodeCount = outputs.Count;
                result.NodeOutputs = outputs;
                result.ExecutionLog.Add($"执行完成，共 {outputs.Count} 个节点");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.ExecutionLog.Add($"执行失败: {ex.Message}");
            }

            return result;
        }

        public async Task<FlowExecutionResult> ExecuteFromNodeAsync(string filePath, Guid nodeInstanceId)
        {
            var result = new FlowExecutionResult();

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var graph = JsonSerializer.Deserialize<FlowGraph>(json);
                if (graph == null)
                {
                    return new FlowExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "无法解析流程文件"
                    };
                }

                var (nodes, connections) = await LoadFlowGraph(graph);
                var startNode = nodes.FirstOrDefault(n => n.InstanceId == nodeInstanceId);

                if (startNode == null)
                {
                    return new FlowExecutionResult
                    {
                        Success = false,
                        ErrorMessage = $"未找到节点实例: {nodeInstanceId}"
                    };
                }

                result.ExecutionLog.Add($"从节点 '{startNode.Title}' 开始执行");

                var executor = new FlowExecutor(_nodeRegistry);
                executor.NodeStateChanged += (nodeId, state) =>
                {
                    var node = nodes.FirstOrDefault(n => n.InstanceId == nodeId);
                    if (node != null)
                    {
                        result.ExecutionLog.Add($"节点 '{node.Title}' 状态: {state}");
                    }
                };

                var outputs = await executor.ExecuteFromNodeAsync(startNode, nodes.ToList(), connections.ToList());

                result.Success = true;
                result.ExecutedNodeCount = outputs.Count;
                result.NodeOutputs = outputs;
                result.ExecutionLog.Add($"执行完成，共 {outputs.Count} 个节点");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<FlowExecutionResult> ExecuteSingleNodeAsync(string filePath, Guid nodeInstanceId)
        {
            var result = new FlowExecutionResult();

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var graph = JsonSerializer.Deserialize<FlowGraph>(json);
                if (graph == null)
                {
                    return new FlowExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "无法解析流程文件"
                    };
                }

                var (nodes, connections) = await LoadFlowGraph(graph);
                var targetNode = nodes.FirstOrDefault(n => n.InstanceId == nodeInstanceId);

                if (targetNode == null)
                {
                    return new FlowExecutionResult
                    {
                        Success = false,
                        ErrorMessage = $"未找到节点实例: {nodeInstanceId}"
                    };
                }

                result.ExecutionLog.Add($"只执行节点 '{targetNode.Title}'");

                var executor = new FlowExecutor(_nodeRegistry);

                var outputs = await executor.ExecuteSingleNodeAsync(targetNode, nodes.ToList(), connections.ToList());

                result.Success = true;
                result.ExecutedNodeCount = outputs.Count;
                result.NodeOutputs = outputs;
                result.ExecutionLog.Add(outputs.Count > 0 ? "执行完成" : "节点未执行");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private async Task<(List<NodeViewModel> Nodes, List<ConnectionViewModel> Connections)> LoadFlowGraph(FlowGraph graph)
        {
            var nodes = new List<NodeViewModel>();
            var connections = new List<ConnectionViewModel>();
            var nodeMap = new Dictionary<Guid, NodeViewModel>();

            foreach (var nd in graph.Nodes)
            {
                var def = _nodeRegistry.CreateInstance(nd.TypeId);
                if (def == null) continue;

                var vm = new NodeViewModel(def, nd.InstanceId);
                vm.Location = new System.Windows.Point(nd.X, nd.Y);

                foreach (var (key, val) in nd.Properties)
                {
                    var type = def.GetType();
                    var prop = type.GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null && prop.CanWrite)
                    {
                        var convertedValue = ConvertJsonValue(val, prop.PropertyType);
                        prop.SetValue(def, convertedValue);
                    }
                    else
                    {
                        var field = type.GetField(key, BindingFlags.Public | BindingFlags.Instance);
                        if (field != null)
                        {
                            var convertedValue = ConvertJsonValue(val, field.FieldType);
                            field.SetValue(def, convertedValue);
                        }
                    }
                }

                nodes.Add(vm);
                nodeMap[nd.InstanceId] = vm;
            }

            foreach (var cd in graph.Connections)
            {
                if (!nodeMap.TryGetValue(cd.SourceNodeId, out var srcVm)) continue;
                if (!nodeMap.TryGetValue(cd.TargetNodeId, out var tgtVm)) continue;

                var srcPort = srcVm.Outputs.FirstOrDefault(p => p.Name == cd.SourcePortName);
                var tgtPort = tgtVm.Inputs.FirstOrDefault(p => p.Name == cd.TargetPortName);

                if (srcPort == null || tgtPort == null) continue;

                var conn = new ConnectionViewModel(srcPort, tgtPort);
                connections.Add(conn);
            }

            return (nodes, connections);
        }

        public async Task<Dictionary<string, FlowExecutionResult>> ExecuteFlowsAsync(IEnumerable<string> filePaths)
        {
            var graphs = new Dictionary<string, FlowGraph>();

            foreach (var filePath in filePaths)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    var graph = JsonSerializer.Deserialize<FlowGraph>(json);
                    if (graph != null)
                    {
                        graphs[filePath] = graph;
                    }
                }
                catch
                {
                    graphs[filePath] = new FlowGraph();
                }
            }

            return await ExecuteFlowsAsync(graphs);
        }

        public async Task<Dictionary<string, FlowExecutionResult>> ExecuteFlowsAsync(Dictionary<string, FlowGraph> graphs)
        {
            var results = new Dictionary<string, FlowExecutionResult>();
            var tasks = new List<Task>();

            foreach (var kv in graphs)
            {
                var key = kv.Key;
                var graph = kv.Value;

                var task = Task.Run(async () =>
                {
                    var result = await ExecuteFlowAsync(graph);
                    lock (results)
                    {
                        results[key] = result;
                    }
                });

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
            return results;
        }

        private static object? ConvertJsonValue(object? value, Type targetType)
        {
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (value is JsonElement element)
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => Convert.ChangeType(element.GetDouble(), targetType),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => value
                };
            }

            return Convert.ChangeType(value, targetType);
        }
    }
}