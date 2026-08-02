using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows.Input;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Framework.Framework;

namespace AFOCS.FlowNodeEditor.ViewModels
{
    /// <summary>
    /// 节点编辑器 Document ViewModel —— 流程编辑器的主 ViewModel
    /// </summary>
    public class NodeEditorDocumentViewModel : PersistedDocument
    {
        private readonly INodeRegistry _nodeRegistry;
        private readonly ReactiveFlowExecutor _reactiveExecutor;

        // ========== 工具箱（左侧） ==========
        public ObservableCollection<ToolboxItemViewModel> ToolboxItems { get; } = [];

        private string _toolboxSearchText = string.Empty;
        public string ToolboxSearchText
        {
            get => _toolboxSearchText;
            set { _toolboxSearchText = value; ApplyToolboxFilter(); }
        }

        private ObservableCollection<ToolboxItemViewModel> _filteredToolboxItems = [];
        public ObservableCollection<ToolboxItemViewModel> FilteredToolboxItems
        {
            get => _filteredToolboxItems;
            set { _filteredToolboxItems = value; NotifyOfPropertyChange(); }
        }

        private void ApplyToolboxFilter()
        {
            var items = string.IsNullOrWhiteSpace(_toolboxSearchText) || _toolboxSearchText.Length < 2
                ? ToolboxItems
                : ToolboxItems.Where(x =>
                    x.DisplayName.Contains(_toolboxSearchText, StringComparison.OrdinalIgnoreCase) ||
                    x.Category.Contains(_toolboxSearchText, StringComparison.OrdinalIgnoreCase));
            FilteredToolboxItems = new ObservableCollection<ToolboxItemViewModel>(items);
        }

        // ========== 节点编辑器（中间） ==========
        public ObservableCollection<NodeViewModel> Nodes { get; } = [];
        public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

        // ========== 属性面板（右侧） ==========
        private ObservableCollection<NodeViewModel> _selectedNodes = [];
        public ObservableCollection<NodeViewModel> SelectedNodes
        {
            get => _selectedNodes;
            set
            {
                _selectedNodes = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(SelectedNode));
            }
        }

        public NodeViewModel? SelectedNode => SelectedNodes.FirstOrDefault();

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
        private double _viewportZoom = 1.0;
        public double ViewportZoom
        {
            get => _viewportZoom;
            set { _viewportZoom = value; NotifyOfPropertyChange(); }
        }

        private System.Windows.Point _viewportLocation;
        public System.Windows.Point ViewportLocation
        {
            get => _viewportLocation;
            set { _viewportLocation = value; NotifyOfPropertyChange(); }
        }

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
        private string _executionStatus = string.Empty;
        public string ExecutionStatus
        {
            get => _executionStatus;
            set { _executionStatus = value; NotifyOfPropertyChange(); }
        }

        public NodeEditorDocumentViewModel(INodeRegistry nodeRegistry)
        {
            _nodeRegistry = nodeRegistry;
            _reactiveExecutor = new ReactiveFlowExecutor(nodeRegistry);
            DisplayName = "FlowGraph";

            _reactiveExecutor.StartListening(Nodes, Connections);

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
                    var related = Connections.Where(c =>
                        c.Output == connector || c.Input == connector).ToList();
                    foreach (var conn in related)
                    {
                        _reactiveExecutor.OnConnectionRemoved(conn, Nodes, Connections);
                    }
                    DisconnectConnector(connector);
                }
            });

            // Nodify 移除连接命令 —— 参数为连接的 DataContext
            RemoveConnectionCommand = new RelayCommand(param =>
            {
                if (param is ConnectionViewModel conn)
                {
                    _reactiveExecutor.OnConnectionRemoved(conn, Nodes, Connections);
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
                _reactiveExecutor.OnConnectionRemoved(existingConn, Nodes, Connections);
                Connections.Remove(existingConn);
            }

            var conn = new ConnectionViewModel(source, target);
            Connections.Add(conn);

            // 触发响应式执行
            _reactiveExecutor.OnConnectionAdded(conn, Nodes, Connections);
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
                var properties = new Dictionary<string, object?>();
                var type = node.Definition.GetType();
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    properties[field.Name] = field.GetValue(node.Definition);
                }
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in props)
                {
                    if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
                    properties[prop.Name] = prop.GetValue(node.Definition);
                }
                graph.Nodes.Add(new FlowNodeData
                {
                    InstanceId = node.InstanceId,
                    TypeId = NodeDefinitionHelper.GetTypeId(node.Definition),
                    X = node.Location.X,
                    Y = node.Location.Y,
                    Properties = properties
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

        private static object? ConvertJsonValue(object? value, Type targetType)
        {
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.String => jsonElement.GetString(),
                    JsonValueKind.Number => ConvertNumber(jsonElement, targetType),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => targetType.IsValueType ? Activator.CreateInstance(targetType) : null,
                    _ => jsonElement.ToString()
                };
            }

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }
        }

        private static object? ConvertNumber(JsonElement element, Type targetType)
        {
            if (targetType == typeof(int) || targetType == typeof(int?))
                return element.GetInt32();
            if (targetType == typeof(long) || targetType == typeof(long?))
                return element.GetInt64();
            if (targetType == typeof(double) || targetType == typeof(double?))
                return element.GetDouble();
            if (targetType == typeof(float) || targetType == typeof(float?))
                return element.GetSingle();
            if (targetType == typeof(decimal) || targetType == typeof(decimal?))
                return element.GetDecimal();
            return element.GetDouble();
        }

        // ========== 流程执行 ==========
        public virtual async Task ExecuteFlowAsync()
        {
            if (Nodes.Count == 0)
            {
                ExecutionStatus = "没有可执行的节点";
                return;
            }

            foreach (var n in Nodes)
                n.ResetExecutionState();

            ExecutionStatus = "正在执行...";
            try
            {
                var executor = new FlowExecutor(_nodeRegistry);

                executor.NodeStateChanged += async (id, state) =>
                {
                    if (state == NodeExecutionState.Executing)
                        await Task.Delay(300);
                };

                var results = await executor.ExecuteAsync(Nodes.ToList(), Connections.ToList());
                ExecutionStatus = $"执行完成，共 {results.Count} 个节点";
            }
            catch (Exception ex)
            {
                ExecutionStatus = $"执行失败: {ex.Message}";
            }
        }

        public virtual async Task ExecuteFromSelectedNodeAsync()
        {
            if (SelectedNode == null)
            {
                ExecutionStatus = "请先选中一个节点";
                return;
            }

            foreach (var n in Nodes)
                n.ResetExecutionState();

            ExecutionStatus = $"从节点 '{SelectedNode.Title}' 开始执行...";
            try
            {
                var executor = new FlowExecutor(_nodeRegistry);

                executor.NodeStateChanged += async (id, state) =>
                {
                    if (state == NodeExecutionState.Executing)
                        await Task.Delay(300);
                };

                var results = await executor.ExecuteFromNodeAsync(SelectedNode, Nodes.ToList(), Connections.ToList());
                ExecutionStatus = $"执行完成，共 {results.Count} 个节点";
            }
            catch (Exception ex)
            {
                ExecutionStatus = $"执行失败: {ex.Message}";
            }
        }

        public virtual async Task ExecuteSingleNodeAsync()
        {
            if (SelectedNode == null)
            {
                ExecutionStatus = "请先选中一个节点";
                return;
            }

            foreach (var n in Nodes)
                n.ResetExecutionState();

            ExecutionStatus = $"执行节点 '{SelectedNode.Title}'...";
            try
            {
                var executor = new FlowExecutor(_nodeRegistry);

                executor.NodeStateChanged += async (id, state) =>
                {
                    if (state == NodeExecutionState.Executing)
                        await Task.Delay(300);
                };

                var results = await executor.ExecuteSingleNodeAsync(SelectedNode, Nodes.ToList(), Connections.ToList());
                ExecutionStatus = results.Count > 0 ? "执行完成" : "节点未执行";
            }
            catch (Exception ex)
            {
                ExecutionStatus = $"执行失败: {ex.Message}";
            }
        }
    }
}
