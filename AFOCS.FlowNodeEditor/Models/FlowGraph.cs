namespace AFOCS.FlowNodeEditor.Models
{
    /// <summary>
    /// 流程图的序列化模型，用于保存/加载
    /// </summary>
    public class FlowGraph
    {
        public List<FlowNodeData> Nodes { get; set; } = [];
        public List<FlowConnectionData> Connections { get; set; } = [];
    }

    public class FlowNodeData
    {
        /// <summary>节点实例唯一 ID</summary>
        public Guid InstanceId { get; set; }

        /// <summary>节点定义 TypeId</summary>
        public string TypeId { get; set; } = string.Empty;

        /// <summary>X 坐标</summary>
        public double X { get; set; }

        /// <summary>Y 坐标</summary>
        public double Y { get; set; }

        /// <summary>节点属性值（属性名 -> 值）</summary>
        public Dictionary<string, object?> Properties { get; set; } = [];
    }

    public class FlowConnectionData
    {
        /// <summary>源节点实例 ID</summary>
        public Guid SourceNodeId { get; set; }

        /// <summary>源端口名称</summary>
        public string SourcePortName { get; set; } = string.Empty;

        /// <summary>目标节点实例 ID</summary>
        public Guid TargetNodeId { get; set; }

        /// <summary>目标端口名称</summary>
        public string TargetPortName { get; set; } = string.Empty;
    }
}
