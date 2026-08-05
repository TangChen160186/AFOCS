using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.FlowNodeEditor.ViewModels;
using AFOCS.VisionEditor.Services;
using Caliburn.Micro;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VisionToolkit.TemplateMatcher;

namespace AFOCS.VisionEditor.ViewModels
{
    /// <summary>
    /// 视觉模板编辑器 Document ViewModel。
    /// 复用流程节点编辑器的界面与交互，只显示视觉节点，并按数据依赖顺序执行。
    /// </summary>
    public class VisionEditorDocumentViewModel : NodeEditorDocumentViewModel
    {
        private readonly IVisionNodeRegistry _visionRegistry;

        public VisionEditorDocumentViewModel(IVisionNodeRegistry visionRegistry)
            : base(visionRegistry)
        {
            _visionRegistry = visionRegistry;
            DisplayName = "视觉模板";
        }

        // ========== 保存：节点定义对象直接序列化（[JsonIgnore] 排除 Mat、算法结果等） ==========

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

        // ========== 执行：按数据依赖顺序执行所有视觉节点 ==========

        public override async Task ExecuteFlowAsync()
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
                var executor = IoC.Get<FlowExecutor>();
                var ordered = GetTopologicalOrder(Nodes, Connections);

                foreach (var node in ordered)
                    await executor.ExecuteSingleNodeAsync(node, Nodes.ToList(), Connections.ToList());

                var summary = BuildResultSummary();
                ExecutionStatus = summary.Length > 0
                    ? summary
                    : (Nodes.Any(n => n.HasError) ? "执行完成（存在失败节点）" : "执行完成");
            }
            catch (Exception ex)
            {
                ExecutionStatus = $"执行失败: {ex.Message}";
            }
        }

        public override async Task ExecuteSingleNodeAsync()
        {
            await base.ExecuteSingleNodeAsync();
            var summary = BuildResultSummary();
            if (summary.Length > 0)
                ExecutionStatus = summary;
        }

        public override async Task ExecuteFromSelectedNodeAsync()
        {
            await base.ExecuteFromSelectedNodeAsync();
            var summary = BuildResultSummary();
            if (summary.Length > 0)
                ExecutionStatus = summary;
        }

        /// <summary>
        /// 对节点按数据连接（非执行端口）做拓扑排序，
        /// 确保依赖的源节点先执行；有环或孤立节点按原顺序兜底。
        /// </summary>
        private static List<NodeViewModel> GetTopologicalOrder(
            IReadOnlyList<NodeViewModel> nodes,
            IReadOnlyList<ConnectionViewModel> connections)
        {
            var dataConns = connections
                .Where(c => c.Output.PortType != NodePortType.Execution)
                .ToList();

            var dependents = nodes.ToDictionary(n => n, _ => new List<NodeViewModel>());
            var inDegree = nodes.ToDictionary(n => n, _ => 0);

            foreach (var conn in dataConns)
            {
                var src = nodes.FirstOrDefault(n => n.InstanceId == conn.Output.ParentInstanceId);
                var dst = nodes.FirstOrDefault(n => n.InstanceId == conn.Input.ParentInstanceId);
                if (src == null || dst == null || ReferenceEquals(src, dst)) continue;
                dependents[src].Add(dst);
                inDegree[dst]++;
            }

            var queue = new Queue<NodeViewModel>(nodes.Where(n => inDegree[n] == 0));
            var result = new List<NodeViewModel>();

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                result.Add(node);
                foreach (var dep in dependents[node])
                {
                    if (--inDegree[dep] == 0)
                        queue.Enqueue(dep);
                }
            }

            foreach (var node in nodes)
            {
                if (!result.Contains(node))
                    result.Add(node);
            }
            return result;
        }

        // ========== 执行结果汇总 ==========

        /// <summary>汇总每个已执行节点的输出端口值，用于状态栏显示</summary>
        private string BuildResultSummary()
        {
            var sb = new StringBuilder();

            foreach (var node in Nodes)
            {
                if (node.HasError)
                {
                    sb.AppendLine($"节点 '{node.Title}' 执行出错");
                    continue;
                }
                if (!node.IsCompleted) continue;

                var parts = new List<string>();
                foreach (var prop in node.Definition.GetType().GetProperties())
                {
                    var portAttr = prop.GetCustomAttribute<NodePortAttribute>();
                    if (portAttr == null || portAttr.IsInput) continue;

                    var value = prop.GetValue(node.Definition);
                    if (value == null) continue;

                    var text = FormatValue(value);
                    if (string.IsNullOrEmpty(text)) continue;

                    parts.Add($"{portAttr.DisplayName}={text}");
                }

                if (parts.Count > 0)
                    sb.AppendLine($"节点 '{node.Title}': {string.Join("  ", parts)}");
            }

            return sb.ToString().TrimEnd();
        }

        private static string FormatValue(object value)
        {
            switch (value)
            {
                case double d when double.IsNaN(d) || double.IsInfinity(d):
                    return "无效";
                case double d:
                    return Math.Round(d, 3).ToString("0.###");
                case float f:
                    return Math.Round(f, 3).ToString("0.###");
                case int i:
                    return i.ToString();
                case bool b:
                    return b ? "是" : "否";
                case string s when s.Length > 60:
                    return s[..60] + "...";
                case System.Drawing.PointF p:
                    return $"({p.X:F1}, {p.Y:F1})";
                case System.Collections.IEnumerable enumerable:
                {
                    var items = enumerable.Cast<object?>().ToList();
                    if (items.Count == 0) return "0项";
                    if (items[0] is MatchResult mr)
                        return $"{items.Count}项 最佳={Math.Round(mr.Score, 3)} @({mr.Center.X:F1}, {mr.Center.Y:F1})";
                    return $"{items.Count}项";
                }
                default:
                    var text = value.ToString();
                    return string.IsNullOrEmpty(text) ? "" : text.Length > 40 ? text[..40] + "..." : text;
            }
        }
    }
}
