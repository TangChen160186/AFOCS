namespace AFOCS.FlowNodeEditor.Models
{
    /// <summary>
    /// 节点数据端口的定义（由 INodeDefinition 描述）
    /// 新节点类型通过在任意 MEF 程序集中导出 INodeDefinition 来注册
    /// </summary>
    public interface INodePortDefinition
    {
        string Name { get; }
        string DisplayName { get; }
        NodePortType PortType { get; }
    }

    /// <summary>
    /// MEF 可导出的节点定义接口。
    /// 新成员创建新工程，实现此接口并加上 [Export(typeof(INodeDefinition))] 即可自动被发现。
    /// </summary>
    public interface INodeDefinition
    {
        /// <summary>节点唯一标识（用于序列化/反序列化）</summary>
        string TypeId { get; }

        /// <summary>显示在工具箱中的名称</summary>
        string DisplayName { get; }

        /// <summary>工具箱分类</summary>
        string Category { get; }

        /// <summary>工具箱图标（可选，null 则不显示图标）</summary>
        Uri? IconSource => null;

        /// <summary>输入端口定义</summary>
        IReadOnlyList<INodePortDefinition> InputPorts { get; }

        /// <summary>输出端口定义</summary>
        IReadOnlyList<INodePortDefinition> OutputPorts { get; }

        /// <summary>节点可编辑属性定义</summary>
        IReadOnlyList<INodePropertyDefinition> Properties { get; }
    }

    /// <summary>
    /// 节点属性定义
    /// </summary>
    public interface INodePropertyDefinition
    {
        string Name { get; }
        string DisplayName { get; }
        NodePropertyValueType ValueType { get; }
        object? DefaultValue { get; }
    }
}
