namespace AFOCS.FlowNodeEditor.Models
{
    /// <summary>
    /// 节点数据端口的定义（由 INodeDefinition 描述）
    /// </summary>
    public interface INodePortDefinition
    {
        string Name { get; }
        string DisplayName { get; }
        NodePortType PortType { get; }
    }

    /// <summary>
    /// MEF 可导出的节点定义接口。
    /// 实现此接口并加上 [Export(typeof(INodeDefinition))] 即可自动被发现。
    /// 端口和属性通过 [NodeInput]/[NodeOutput]/[NodeProperty] 自定义 Attribute 声明，
    /// 框架通过 NodeDefinitionScanner 反射自动发现，无需手动实现列表属性。
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
