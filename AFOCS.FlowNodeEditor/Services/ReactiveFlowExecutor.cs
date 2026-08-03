using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.ViewModels;

namespace AFOCS.FlowNodeEditor.Services;

public class ReactiveFlowExecutor
{
    private readonly INodeRegistry _registry;
    private readonly HashSet<Guid> _updating = [];
    private bool _isEnabled = true;
    private IReadOnlyList<NodeViewModel>? _currentNodes;
    private IReadOnlyList<ConnectionViewModel>? _currentConnections;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public ReactiveFlowExecutor(INodeRegistry registry)
    {
        _registry = registry;
    }

    public void StartListening(ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections)
    {
        _currentNodes = nodes;
        _currentConnections = connections;

        nodes.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is NodeViewModel node)
                        StartListeningToNode(node);
                }
            }
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is NodeViewModel node)
                        StopListeningToNode(node);
                }
            }
        };

        foreach (var node in nodes)
            StartListeningToNode(node);
    }

    private void StartListeningToNode(NodeViewModel node)
    {
        if (node.Definition is INotifyPropertyChanged notifyNode)
        {
            notifyNode.PropertyChanged += (_, e) =>
            {
                if (!_isEnabled) return;
                if (_updating.Contains(node.InstanceId)) return;
                OnNodePropertyChanged(node, e.PropertyName);
            };
        }
    }

    private void StopListeningToNode(NodeViewModel node)
    {
        if (node.Definition is INotifyPropertyChanged notifyNode)
        {
            notifyNode.PropertyChanged -= (_, _) => { };
        }
    }

    private void OnNodePropertyChanged(NodeViewModel node, string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return;
        if (!IsDataNode(node)) return;

        if (_currentNodes == null || _currentConnections == null) return;

        ExecuteNode(node, _currentNodes, _currentConnections);

        var portAttr = node.Definition.GetType()
            .GetProperty(propertyName)
            ?.GetCustomAttribute<NodePortAttribute>();

        if (portAttr != null && !portAttr.IsInput)
        {
            PropagateOutputChange(node, propertyName);
        }
        else
        {
            PropagateAllOutputChanges(node);
        }
    }

    private void PropagateAllOutputChanges(NodeViewModel node)
    {
        var outputProperties = node.Definition.GetType().GetProperties()
            .Where(p => p.GetCustomAttribute<NodePortAttribute>()?.IsInput == false)
            .ToList();

        foreach (var prop in outputProperties)
        {
            var portAttr = prop.GetCustomAttribute<NodePortAttribute>();
            if (portAttr != null)
            {
                PropagateOutputChange(node, portAttr.Name, prop);
            }
        }
    }

    public void OnConnectionAdded(ConnectionViewModel connection, IReadOnlyList<NodeViewModel> nodes, IReadOnlyList<ConnectionViewModel> connections)
    {
        if (!_isEnabled) return;

        var inputNode = nodes.FirstOrDefault(n => n.Inputs.Contains(connection.Input));
        if (inputNode == null) return;

        var outputNode = nodes.FirstOrDefault(n => n.Outputs.Contains(connection.Output));
        if (outputNode != null)
        {
            ExecuteDataNodeIfReady(outputNode, nodes, connections);
        }

        ExecuteDataNodeIfReady(inputNode, nodes, connections);
    }

    public void OnConnectionRemoved(ConnectionViewModel connection, IReadOnlyList<NodeViewModel> nodes, IReadOnlyList<ConnectionViewModel> connections)
    {
        if (!_isEnabled) return;

        var inputNode = nodes.FirstOrDefault(n => n.Inputs.Contains(connection.Input));
        if (inputNode != null)
        {
            ExecuteDataNodeIfReady(inputNode, nodes, connections);
        }
    }

    private void PropagateOutputChange(NodeViewModel sourceNode, string outputName, PropertyInfo? property = null)
    {
        if (_updating.Contains(sourceNode.InstanceId)) return;

        _updating.Add(sourceNode.InstanceId);
        try
        {
            property ??= sourceNode.Definition.GetType().GetProperty(outputName);
            var value = property?.GetValue(sourceNode.Definition);

            if (_currentNodes == null || _currentConnections == null) return;

            var downstreamNodes = FindDownstreamNodes(sourceNode, outputName, _currentNodes, _currentConnections);

            foreach (var targetNode in downstreamNodes)
            {
                UpdateInputPort(targetNode, outputName, value);
                ExecuteDataNodeIfReady(targetNode, _currentNodes, _currentConnections);
            }
        }
        finally
        {
            _updating.Remove(sourceNode.InstanceId);
        }
    }

    private List<NodeViewModel> FindDownstreamNodes(
        NodeViewModel sourceNode,
        string outputName,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections)
    {
        var result = new List<NodeViewModel>();
        var visited = new HashSet<Guid>();

        var outputConnector = sourceNode.Outputs.FirstOrDefault(o => o.Name == outputName);
        if (outputConnector == null) return result;

        var queue = new Queue<NodeViewModel>();
        var connectedConns = connections.Where(c => c.Output == outputConnector).ToList();

        foreach (var conn in connectedConns)
        {
            var targetNode = nodes.FirstOrDefault(n => n.Inputs.Contains(conn.Input));
            if (targetNode != null && !visited.Contains(targetNode.InstanceId))
            {
                visited.Add(targetNode.InstanceId);
                queue.Enqueue(targetNode);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var output in current.Outputs)
            {
                if (output.PortType == NodePortType.Execution) continue;

                var downstreamConns = connections.Where(c => c.Output == output).ToList();
                foreach (var conn in downstreamConns)
                {
                    var nextNode = nodes.FirstOrDefault(n => n.Inputs.Contains(conn.Input));
                    if (nextNode != null && !visited.Contains(nextNode.InstanceId))
                    {
                        visited.Add(nextNode.InstanceId);
                        queue.Enqueue(nextNode);
                    }
                }
            }
        }

        return result;
    }

    private void UpdateInputPort(NodeViewModel node, string sourcePortName, object? value)
    {
        var portAttr = node.Definition.GetType()
            .GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<NodePortAttribute>()?.Name == sourcePortName);

        if (portAttr != null)
        {
            portAttr.SetValue(node.Definition, value);
        }
    }

    private PropertyInfo? GetPropertyByPortName(INodeDefinition definition, string portName)
    {
        return definition.GetType().GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<NodePortAttribute>()?.Name == portName);
    }

    private void ExecuteDataNodeIfReady(
        NodeViewModel node,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections)
    {
        if (!IsDataNode(node)) return;
        if (_updating.Contains(node.InstanceId)) return;

        var allInputsConnected = AreAllInputsConnected(node, connections);

        if (!allInputsConnected)
        {
            var canExecuteWithDefaults = CanExecuteWithDefaults(node);
            if (!canExecuteWithDefaults) return;
        }

        ExecuteNode(node, nodes, connections);
    }

    private bool IsDataNode(NodeViewModel node)
    {
        return !node.Inputs.Any(i => i.PortType == NodePortType.Execution) &&
               !node.Outputs.Any(o => o.PortType == NodePortType.Execution);
    }

    private bool AreAllInputsConnected(NodeViewModel node, IReadOnlyList<ConnectionViewModel> connections)
    {
        var dataInputs = node.Inputs.Where(i => i.PortType != NodePortType.Execution).ToList();
        if (dataInputs.Count == 0) return true;

        foreach (var input in dataInputs)
        {
            if (!connections.Any(c => c.Input == input))
                return false;
        }
        return true;
    }

    private bool CanExecuteWithDefaults(NodeViewModel node)
    {
        return true;
    }

    private void ExecuteNode(
        NodeViewModel node,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections)
    {
        if (_updating.Contains(node.InstanceId)) return;

        _updating.Add(node.InstanceId);
        try
        {
            foreach (var input in node.Inputs)
            {
                if (input.PortType == NodePortType.Execution) continue;

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

            if (node.Definition is IExecutableNode execNode)
            {
                var context = new Dictionary<string, object?>();
                var outputs = execNode.ExecuteAsync(context).Result;

                foreach (var kv in outputs)
                {
                    var prop = node.Definition.GetType().GetProperty(kv.Key);
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(node.Definition, kv.Value);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[Reactive] 节点 '{node.Title}' 响应式执行完成");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Reactive] 节点 '{node.Title}' 执行失败: {ex.Message}");
        }
        finally
        {
            _updating.Remove(node.InstanceId);
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