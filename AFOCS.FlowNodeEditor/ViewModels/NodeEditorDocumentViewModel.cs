using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Framework.Framework;
using Caliburn.Micro;

namespace AFOCS.FlowNodeEditor.ViewModels
{
    /// <summary>
    /// 节点编辑器 Document ViewModel —— 流程编辑器的主 ViewModel
    /// </summary>
    public class NodeEditorDocumentViewModel : PersistedDocument
    {
        private readonly INodeRegistry _nodeRegistry;

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
        private NodeViewModel? _selectedNode;
        public NodeViewModel? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode == value) return;
                if (_selectedNode != null)
                    _selectedNode.IsSelected = false;
                _selectedNode = value;
                if (_selectedNode != null)
                    _selectedNode.IsSelected = true;
                NotifyOfPropertyChange();
            }
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
            DisplayName = "FlowGraph";

            DeleteSelectedNodeCommand = new RelayCommand(_ =>
            {
                if (SelectedNode == null) return;
                var related = Connections.Where(c =>
                    c.Output.ParentInstanceId == SelectedNode.InstanceId ||
                    c.Input.ParentInstanceId == SelectedNode.InstanceId).ToList();
                foreach (var conn in related)
                {
                    conn.Output.IsConnected = false;
                    conn.Input.IsConnected = false;
                    Connections.Remove(conn);
                }
                Nodes.Remove(SelectedNode);
                SelectedNode = null;
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
                SelectedNode = null;
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
                    DisconnectConnector(connector);
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
            SelectedNode = vm;
            IsDirty = true;
        }

        // ========== 连接操作 ==========
        public bool CanConnect(ConnectorViewModel source, ConnectorViewModel target)
        {
            // 必须一输一出
            if (source.IsInput == target.IsInput) return false;
            // 不能自连
            if (source.ParentInstanceId == target.ParentInstanceId) return false;
            // 已连接的端口不能再连
            if (source.IsConnected || target.IsConnected) return false;

            // 端口类型校验
            var output = source.IsInput ? target : source;
            var input = source.IsInput ? source : target;

            if (!IsPortTypeCompatible(output.PortType, input.PortType))
                return false;

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
            SelectedNode = null;
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
                var def = _nodeRegistry.GetDefinition(nd.TypeId);
                if (def == null) continue;
                var vm = new NodeViewModel(def, nd.InstanceId)
                {
                    Location = new System.Windows.Point(nd.X, nd.Y)
                };
                foreach (var (key, val) in nd.Properties)
                {
                    if (vm.PropertyValues.ContainsKey(key))
                        vm.PropertyValues[key] = val;
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
                graph.Nodes.Add(new FlowNodeData
                {
                    InstanceId = node.InstanceId,
                    TypeId = node.Definition.TypeId,
                    X = node.Location.X,
                    Y = node.Location.Y,
                    Properties = new Dictionary<string, object?>(node.PropertyValues)
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
        public async Task ExecuteFlowAsync()
        {
            if (Nodes.Count == 0)
            {
                ExecutionStatus = "没有可执行的节点";
                return;
            }

            ExecutionStatus = "正在执行...";
            try
            {
                var executor = new FlowExecutor(_nodeRegistry);
                var results = await executor.ExecuteAsync(Nodes.ToList(), Connections.ToList());
                ExecutionStatus = $"执行完成，共 {results.Count} 个节点";
            }
            catch (Exception ex)
            {
                ExecutionStatus = $"执行失败: {ex.Message}";
            }
        }
    }
}
