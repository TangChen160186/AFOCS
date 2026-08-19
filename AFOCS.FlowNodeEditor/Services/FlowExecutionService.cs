using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.IO;
using System.Text.Json;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.ViewModels;
using AFOCS.Infrastructure;
using Caliburn.Micro;

namespace AFOCS.FlowNodeEditor.Services;

public interface IFlowExecutionService
{
    Task<FlowExecutionResult> ExecuteFlowAsync(string filePath, WorkPos workPos = WorkPos.Left, bool reportResult = true);
    Task<FlowExecutionResult> ExecuteFlowAsync(FlowGraph graph, WorkPos workPos = WorkPos.Left, bool reportResult = true);
    Task<FlowExecutionResult> ExecuteFromNodeAsync(string filePath, Guid nodeInstanceId, WorkPos workPos = WorkPos.Left);
    Task<FlowExecutionResult> ExecuteSingleNodeAsync(string filePath, Guid nodeInstanceId, WorkPos workPos = WorkPos.Left);

    /// <summary>取消指定工位正在执行的流程（外部急停 / 取消按钮触发）</summary>
    void CancelExecution(WorkPos workPos, bool emergency);
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
    private readonly IEventAggregator _eventAggregator;

    /// <summary>各工位当前正在执行的执行器（急停 / 取消时用于中止流程）</summary>
    private readonly ConcurrentDictionary<WorkPos, FlowExecutor> _activeExecutors = new();

    /// <summary>各工位当前正在执行的流程文件名（用于状态消息显示）</summary>
    private readonly ConcurrentDictionary<WorkPos, string> _activeFileNames = new();

    /// <summary>各工位最近一次取消是否为急停（用于结束状态消息保持急停 / 取消语义）</summary>
    private readonly ConcurrentDictionary<WorkPos, bool> _cancelEmergency = new();

    [ImportingConstructor]
    public FlowExecutionService(INodeRegistry nodeRegistry, IEventAggregator eventAggregator)
    {
        _nodeRegistry = nodeRegistry;
        _eventAggregator = eventAggregator;
    }

    public void CancelExecution(WorkPos workPos, bool emergency)
    {
        _cancelEmergency[workPos] = emergency;

        if (_activeExecutors.TryGetValue(workPos, out var executor))
            executor.Cancel();

        _ = _eventAggregator.PublishOnUIThreadAsync(new FlowExecutionStateMessage
        {
            WorkPos = workPos,
            Status = emergency ? FlowExecutionStatus.EmergencyStopped : FlowExecutionStatus.Cancelled,
            FileName = GetActiveFileName(workPos),
        });
    }

    private string GetActiveFileName(WorkPos workPos)
        => _activeFileNames.TryGetValue(workPos, out var name) ? name : string.Empty;

    public async Task<FlowExecutionResult> ExecuteFlowAsync(string filePath, WorkPos workPos = WorkPos.Left, bool reportResult = true)
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

            // 记录当前流程文件名，供状态消息显示
            _activeFileNames[workPos] = Path.GetFileName(filePath);
            return await ExecuteFlowAsync(graph, workPos, reportResult);
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

    public async Task<FlowExecutionResult> ExecuteFlowAsync(FlowGraph graph, WorkPos workPos = WorkPos.Left, bool reportResult = true)
    {
        var result = new FlowExecutionResult();
        FlowExecutor? executor = null;

        try
        {
            var (nodes, connections) = await LoadFlowGraph(graph);
            result.ExecutionLog.Add($"加载流程: {nodes.Count} 个节点, {connections.Count} 条连线");

            executor = IoC.Get<FlowExecutor>();
            executor.NodeStateChanged += (nodeId, state) =>
            {
                var node = nodes.FirstOrDefault(n => n.InstanceId == nodeId);
                if (node != null)
                {
                    result.ExecutionLog.Add($"节点 '{node.Title}' 状态: {state}");
                }
            };

            // 注册为当前工位的活动执行器，供外部急停 / 取消中止流程
            _activeExecutors[workPos] = executor;

            // 发布"运行中"状态
            _ = _eventAggregator.PublishOnUIThreadAsync(new FlowExecutionStateMessage
            {
                WorkPos = workPos,
                Status = FlowExecutionStatus.Running,
                FileName = GetActiveFileName(workPos),
            });

            try
            {
                var outputs = await executor.ExecuteAsync(nodes.ToList(), connections.ToList(), workPos);

                if (executor.WasCancelled)
                {
                    result.Success = false;
                    result.ErrorMessage = "执行已取消";
                    result.ExecutionLog.Add("执行被取消");
                }
                else
                {
                    result.Success = true;
                    result.ExecutedNodeCount = outputs.Count;
                    result.NodeOutputs = outputs;
                    result.ExecutionLog.Add($"执行完成，共 {outputs.Count} 个节点");
                }
            }
            finally
            {
                _activeExecutors.TryRemove(workPos, out _);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = GetErrorMessage(ex);
            result.ExecutionLog.Add($"执行失败: {GetErrorMessage(ex)}");
        }

        // 发布结束状态：被急停 / 取消的流程保持急停 / 取消语义，其余按成功 / 失败
        FlowExecutionStatus endStatus;
        if (executor?.WasCancelled == true)
        {
            var emergency = _cancelEmergency.TryRemove(workPos, out var e) && e;
            endStatus = emergency ? FlowExecutionStatus.EmergencyStopped : FlowExecutionStatus.Cancelled;
        }
        else
        {
            _cancelEmergency.TryRemove(workPos, out _);
            endStatus = result.Success ? FlowExecutionStatus.Completed : FlowExecutionStatus.Error;
        }

        _ = _eventAggregator.PublishOnUIThreadAsync(new FlowExecutionStateMessage
        {
            WorkPos = workPos,
            Status = endStatus,
            FileName = GetActiveFileName(workPos),
            Message = result.ErrorMessage,
        });

        _activeFileNames.TryRemove(workPos, out _);

        if (reportResult)
        {
            _ = _eventAggregator.PublishOnUIThreadAsync(new FlowExecutionCompletedMessage
            {
                WorkPos = workPos,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
            });
        }

        return result;
    }

    public async Task<FlowExecutionResult> ExecuteFromNodeAsync(string filePath, Guid nodeInstanceId, WorkPos workPos = WorkPos.Left)
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

            var executor = IoC.Get<FlowExecutor>();
            executor.NodeStateChanged += (nodeId, state) =>
            {
                var node = nodes.FirstOrDefault(n => n.InstanceId == nodeId);
                if (node != null)
                {
                    result.ExecutionLog.Add($"节点 '{node.Title}' 状态: {state}");
                }
            };

            executor.SetWorkPos(workPos);
            var outputs = await executor.ExecuteFromNodeAsync(startNode, nodes.ToList(), connections.ToList());

            result.Success = true;
            result.ExecutedNodeCount = outputs.Count;
            result.NodeOutputs = outputs;
            result.ExecutionLog.Add($"执行完成，共 {outputs.Count} 个节点");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = GetErrorMessage(ex);
        }

        return result;
    }

    public async Task<FlowExecutionResult> ExecuteSingleNodeAsync(string filePath, Guid nodeInstanceId, WorkPos workPos = WorkPos.Left)
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

            var executor = IoC.Get<FlowExecutor>();
            executor.SetWorkPos(workPos);
            var outputs = await executor.ExecuteSingleNodeAsync(targetNode, nodes.ToList(), connections.ToList());

            result.Success = true;
            result.ExecutedNodeCount = outputs.Count;
            result.NodeOutputs = outputs;
            result.ExecutionLog.Add(outputs.Count > 0 ? "执行完成" : "节点未执行");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = GetErrorMessage(ex);
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

            // 按属性声明类型还原（类型转换由 System.Text.Json 处理）
            NodeDefinitionHelper.ApplySerialized(def, nd.Serialized);

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

    /// <summary>展开聚合异常，取最内层真实错误消息</summary>
    private static string GetErrorMessage(Exception ex)
    {
        var current = ex;
        while (current is AggregateException agg && agg.InnerExceptions.Count == 1)
            current = agg.InnerException!;
        return current.Message;
    }
}