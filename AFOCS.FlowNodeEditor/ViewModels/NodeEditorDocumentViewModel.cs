using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Framework.Framework;
using AFOCS.Infrastructure;
using Caliburn.Micro;

namespace AFOCS.FlowNodeEditor.ViewModels;

/// <summary>
/// 节点编辑器 Document ViewModel —— 流程编辑器的主 ViewModel
/// </summary>

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class NodeEditorDocumentViewModel : PersistedDocument
{
    private readonly INodeRegistry _nodeRegistry;

    // ========== 工具箱（左侧） ==========
    public ObservableCollection<ToolboxItemViewModel> ToolboxItems { get; } = [];

    public string ToolboxSearchText
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                ApplyToolboxFilter();
            }
        }
    }
    private void ApplyToolboxFilter()
    {
        var items = string.IsNullOrWhiteSpace(ToolboxSearchText) || ToolboxSearchText.Length < 1
            ? ToolboxItems
            : ToolboxItems.Where(x =>
                x.DisplayName.Contains(ToolboxSearchText, StringComparison.OrdinalIgnoreCase) ||
                x.Category.Contains(ToolboxSearchText, StringComparison.OrdinalIgnoreCase));
        FilteredToolboxItems = new ObservableCollection<ToolboxItemViewModel>(items);
    }

    public ObservableCollection<ToolboxItemViewModel> FilteredToolboxItems
    {
        get;
        set => Set(ref field, value);
    }


    // ========== 节点编辑器（中间） ==========
    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    // ========== 属性面板（右侧） ==========
    public NodeViewModel? SelectedNode => SelectedNodes.FirstOrDefault();

    public ObservableCollection<NodeViewModel> SelectedNodes
    {
        get;
        set
        {
            if (Set(ref field, value))
                NotifyOfPropertyChange(nameof(SelectedNode));
        }
    } = [];



    public void SelectNode(NodeViewModel node)
    {
        if (!SelectedNodes.Contains(node))
        {
            SelectedNodes.Add(node);
            node.IsSelected = true;
            NotifyOfPropertyChange(nameof(SelectedNode));
        }
    }

    public void DeselectNode(NodeViewModel node)
    {
        if (SelectedNodes.Remove(node))
        {
            node.IsSelected = false;
            NotifyOfPropertyChange(nameof(SelectedNode));
        }
    }

    public void ClearSelection()
    {
        foreach (var node in SelectedNodes.ToList())
            node.IsSelected = false;
        SelectedNodes.Clear();
        NotifyOfPropertyChange(nameof(SelectedNode));
    }

    public void ToggleSelectNode(NodeViewModel node)
    {
        if (SelectedNodes.Contains(node))
            DeselectNode(node);
        else
            SelectNode(node);
    }

    // ========== 缩放和平移 ==========
    public double ViewportZoom
    {
        get;
        set => Set(ref field, value);
    } = 1.0f;

    
    public Point ViewportLocation
    {
        get;
        set => Set(ref field, value);
    }
    public string ExecutionStatus
    {
        get;
        set => Set(ref field, value);
    }

    /// <summary>当前执行是否出错（状态栏红色提示）</summary>
    public bool HasExecutionError
    {
        get;
        set => Set(ref field, value);
    }

    /// <summary>设置状态栏文字并同步错误标志</summary>
    protected void SetExecutionStatus(string message, bool isError = false)
    {
        ExecutionStatus = message;
        HasExecutionError = isError;
    }

    // ========== 全局工位选择 ==========

    /// <summary>全局工位选择，覆盖 EntryNodeDefinition 的独立 Workpos 设置</summary>
    public WorkPos GlobalWorkPos
    {
        get;
        set => Set(ref field, value);
    } = WorkPos.Left;

    /// <summary>工位选项列表（供 ComboBox 绑定）</summary>
    public IReadOnlyList<WorkPosItem> WorkPosOptions { get; } =
    [
        new WorkPosItem(WorkPos.Left, "左工位"),
        new WorkPosItem(WorkPos.Right, "右工位"),
    ];

    /// <summary>工位下拉项</summary>
    public record WorkPosItem(WorkPos Value, string DisplayName);

    // ========== 命令 ==========
    public ICommand DeleteSelectedNodeCommand { get; }
    public ICommand DeleteSelectedConnectionCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand ConnectionCompletedCommand { get; }
    public ICommand DisconnectConnectorCommand { get; }
    public ICommand RemoveConnectionCommand { get; }
    public ICommand ExecuteCommand { get; }
    public ICommand ExecuteFromSelectedNodeCommand { get; }
    public ICommand ExecuteSingleNodeCommand { get; }

    // ========== 执行状态 ==========

    [ImportingConstructor]
    public NodeEditorDocumentViewModel(INodeRegistry nodeRegistry)
    {
        _nodeRegistry = nodeRegistry;

        DeleteSelectedNodeCommand = new RelayCommand(_ =>
        {
            if (SelectedNodes.Count == 0) return;
            var selectedIds = SelectedNodes.Select(n => n.InstanceId).ToHashSet();
            var related = Connections.Where(c =>
                selectedIds.Contains(c.Output.ParentInstanceId) ||
                selectedIds.Contains(c.Input.ParentInstanceId)).ToList();
            foreach (var conn in related)
            {
                conn.Output.IsConnected = false;
                conn.Input.IsConnected = false;
                Connections.Remove(conn);
            }
            foreach (var node in SelectedNodes.ToList())
                Nodes.Remove(node);
            ClearSelection();
            IsDirty = true;
        });

        DeleteSelectedConnectionCommand = new RelayCommand(_ =>
        {
            if (SelectedConnection == null) return;
            SelectedConnection.Output.IsConnected = false;
            SelectedConnection.Input.IsConnected = false;
            Connections.Remove(SelectedConnection);
            SelectedConnection = null;
            IsDirty = true;
        });

        ClearAllCommand = new RelayCommand(_ =>
        {
            foreach (var conn in Connections)
            {
                conn.Output.IsConnected = false;
                conn.Input.IsConnected = false;
            }
            Nodes.Clear();
            Connections.Clear();
            ClearSelection();
            IsDirty = true;
        });

        // Nodify 连接完成命令 —— 参数为 (object Source, object? Target) 元组
        ConnectionCompletedCommand = new RelayCommand(param =>
        {
            if (param is not ValueTuple<object, object?> tuple) return;
            var source = tuple.Item1 as ConnectorViewModel;
            var target = tuple.Item2 as ConnectorViewModel;
            if (source == null || target == null) return;
            AddConnection(source, target);
        });

        // Nodify 断开连接器命令 —— 参数为连接器自身
        DisconnectConnectorCommand = new RelayCommand(param =>
        {
            if (param is ConnectorViewModel connector)
            {
                DisconnectConnector(connector);
            }
        });

        // Nodify 移除连接命令 —— 参数为连接的 DataContext
        RemoveConnectionCommand = new RelayCommand(param =>
        {
            if (param is ConnectionViewModel conn)
            {
                conn.Output.IsConnected = false;
                conn.Input.IsConnected = false;
                Connections.Remove(conn);
                IsDirty = true;
            }
        });

        // 执行流程图
        ExecuteCommand = new RelayCommand(_ =>
        {
            _ = ExecuteFlowAsync();
        });

        // 从选中节点开始执行
        ExecuteFromSelectedNodeCommand = new RelayCommand(_ =>
        {
            _ = ExecuteFromSelectedNodeAsync();
        });

        // 只执行当前选中节点
        ExecuteSingleNodeCommand = new RelayCommand(_ =>
        {
            _ = ExecuteSingleNodeAsync();
        });
    }

    // ========== 选中连接 ==========
    private ConnectionViewModel? _selectedConnection;
    public ConnectionViewModel? SelectedConnection
    {
        get => _selectedConnection;
        set { _selectedConnection = value; NotifyOfPropertyChange(); }
    }

    // ========== 工具箱初始化 ==========
    public void InitializeToolbox()
    {
        ToolboxItems.Clear();
        foreach (var def in _nodeRegistry.AllDefinitions)
            ToolboxItems.Add(new ToolboxItemViewModel(def));
        ApplyToolboxFilter();
    }

    // ========== 从工具箱添加节点 ==========
    public void AddNodeFromToolbox(ToolboxItemViewModel item)
    {
        if (item == null) return;
        var vm = item.CreateNodeViewModel();
        vm.Location = new System.Windows.Point(300, 300);
        Nodes.Add(vm);
        ClearSelection();
        SelectNode(vm);
        IsDirty = true;
    }

    // ========== 连接操作 ==========
    public bool CanConnect(ConnectorViewModel source, ConnectorViewModel target)
    {
        // 必须一输一出
        if (source.IsInput == target.IsInput) return false;
        // 不能自连
        if (source.ParentInstanceId == target.ParentInstanceId) return false;

        // 端口类型校验
        var output = source.IsInput ? target : source;
        var input = source.IsInput ? source : target;

        if (!IsPortTypeCompatible(output.PortType, input.PortType))
            return false;

        // 输入端口只能有一个连接，但允许重新连接（会先断开旧连接）
        // 输出端口可以连接多个输入
        return true;
    }

    /// <summary>检查输出端口类型是否兼容输入端口类型</summary>
    private static bool IsPortTypeCompatible(NodePortType output, NodePortType input)
    {
        // Execution 只能连 Execution
        if (output == NodePortType.Execution || input == NodePortType.Execution)
            return output == NodePortType.Execution && input == NodePortType.Execution;

        // Any 输入接受任何类型，Any 输出可以连任何输入
        if (input == NodePortType.Any || output == NodePortType.Any)
            return true;

        // 同类型兼容
        if (output == input) return true;

        // Int 可连 Double（自动扩展）
        if (output == NodePortType.Int && input == NodePortType.Double) return true;

        return false;
    }

    public void AddConnection(ConnectorViewModel source, ConnectorViewModel target)
    {
        if (!CanConnect(source, target)) return;

        if (source.IsInput) (source, target) = (target, source);

        // 如果输入端口已经有连接，先断开旧连接
        var existingConn = Connections.FirstOrDefault(c => c.Input == target);
        if (existingConn != null)
        {
            Connections.Remove(existingConn);
        }

        var conn = new ConnectionViewModel(source, target);
        Connections.Add(conn);
        IsDirty = true;
    }

    public void DisconnectConnector(ConnectorViewModel connector)
    {
        var related = Connections.Where(c =>
            c.Output == connector || c.Input == connector).ToList();
        foreach (var conn in related)
        {
            conn.Output.IsConnected = false;
            conn.Input.IsConnected = false;
            Connections.Remove(conn);
        }
        IsDirty = true;
    }

    // ========== 持久化 ==========
    protected override Task DoNew()
    {
        Nodes.Clear();
        Connections.Clear();
        ClearSelection();
        InitializeToolbox();
        return Task.CompletedTask;
    }

    protected override async Task DoLoad(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var graph = JsonSerializer.Deserialize<FlowGraph>(json);
        if (graph == null) return;

        Nodes.Clear();
        Connections.Clear();

        var nodeMap = new Dictionary<Guid, NodeViewModel>();
        foreach (var nd in graph.Nodes)
        {
            var def = _nodeRegistry.CreateInstance(nd.TypeId);
            if (def == null) continue;
            var vm = new NodeViewModel(def, nd.InstanceId)
            {
                Location = new System.Windows.Point(nd.X, nd.Y)
            };

            // 按属性声明类型还原（类型转换由 System.Text.Json 处理）
            NodeDefinitionHelper.ApplySerialized(def, nd.Serialized);

            Nodes.Add(vm);
            nodeMap[nd.InstanceId] = vm;
        }

        foreach (var cd in graph.Connections)
        {
            if (!nodeMap.TryGetValue(cd.SourceNodeId, out var srcVm)) continue;
            if (!nodeMap.TryGetValue(cd.TargetNodeId, out var tgtVm)) continue;
            var srcPort = srcVm.Outputs.FirstOrDefault(p => p.Name == cd.SourcePortName);
            var tgtPort = tgtVm.Inputs.FirstOrDefault(p => p.Name == cd.TargetPortName);
            if (srcPort == null || tgtPort == null) continue;
            if (CanConnect(srcPort, tgtPort))
                AddConnection(srcPort, tgtPort);
        }

        InitializeToolbox();
    }

    protected override async Task DoSave(string filePath)
    {
        var graph = new FlowGraph();

        foreach (var node in Nodes)
        {
            graph.Nodes.Add(new FlowNodeData
            {
                InstanceId = node.InstanceId,
                TypeId = NodeDefinitionHelper.GetTypeId(node.Definition),
                X = node.Location.X,
                Y = node.Location.Y,
                Serialized = JsonSerializer.Serialize(node.Definition, node.Definition.GetType())
            });
        }

        foreach (var conn in Connections)
        {
            graph.Connections.Add(new FlowConnectionData
            {
                SourceNodeId = conn.Output.ParentInstanceId,
                SourcePortName = conn.Output.Name,
                TargetNodeId = conn.Input.ParentInstanceId,
                TargetPortName = conn.Input.Name
            });
        }

        var json = JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    // ========== 流程执行 ==========
    public virtual async Task ExecuteFlowAsync()
    {
        if (Nodes.Count == 0)
        {
            SetExecutionStatus("没有可执行的节点");
            return;
        }

        foreach (var n in Nodes)
            n.ResetExecutionState();

        SetExecutionStatus("正在执行...");
        try
        {
            var executor = IoC.Get<FlowExecutor>();

            executor.NodeStateChanged += async (id, state) =>
            {
                if (state == NodeExecutionState.Executing)
                    await Task.Delay(300);
            };

            var results = await executor.ExecuteAsync(Nodes.ToList(), Connections.ToList(), GlobalWorkPos);
            SetExecutionStatus($"执行完成，共 {results.Count} 个节点");
        }
        catch (Exception ex)
        {
            SetExecutionStatus($"执行失败: {GetExecutionError(ex)}", isError: true);
        }
    }

    public virtual async Task ExecuteFromSelectedNodeAsync()
    {
        if (SelectedNode == null)
        {
            SetExecutionStatus("请先选中一个节点");
            return;
        }

        foreach (var n in Nodes)
            n.ResetExecutionState();

        SetExecutionStatus($"从节点 '{SelectedNode.Title}' 开始执行...");
        try
        {
            var executor = IoC.Get<FlowExecutor>();

            executor.NodeStateChanged += async (id, state) =>
            {
                if (state == NodeExecutionState.Executing)
                    await Task.Delay(300);
            };

            // 设置全局工位后再从选中节点执行
            executor.SetWorkPos(GlobalWorkPos);
            var results = await executor.ExecuteFromNodeAsync(SelectedNode, Nodes.ToList(), Connections.ToList());
            SetExecutionStatus($"执行完成，共 {results.Count} 个节点");
        }
        catch (Exception ex)
        {
            SetExecutionStatus($"执行失败: {GetExecutionError(ex)}", isError: true);
        }
    }

    public virtual async Task ExecuteSingleNodeAsync()
    {
        if (SelectedNode == null)
        {
            SetExecutionStatus("请先选中一个节点");
            return;
        }

        foreach (var n in Nodes)
            n.ResetExecutionState();

        SetExecutionStatus($"执行节点 '{SelectedNode.Title}'...");
        try
        {
            var executor = IoC.Get<FlowExecutor>();

            executor.NodeStateChanged += async (id, state) =>
            {
                if (state == NodeExecutionState.Executing)
                    await Task.Delay(300);
            };

            // 设置全局工位
            executor.SetWorkPos(GlobalWorkPos);
            var results = await executor.ExecuteSingleNodeAsync(SelectedNode, Nodes.ToList(), Connections.ToList());
            SetExecutionStatus(results.Count > 0 ? "执行完成" : "节点未执行");
        }
        catch (Exception ex)
        {
            SetExecutionStatus($"执行失败: {GetExecutionError(ex)}", isError: true);
        }
    }

    /// <summary>展开聚合异常，取最内层真实错误消息</summary>
    private static string GetExecutionError(Exception ex)
    {
        var current = ex;
        while (current is AggregateException agg && agg.InnerExceptions.Count == 1)
            current = agg.InnerException!;
        return current.Message;
    }

}
