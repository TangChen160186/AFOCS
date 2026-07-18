namespace AFOCS.FlowNodeEditor.Models
{
    /// <summary>
    /// 标记节点的输入端口。可标注在类上（Execution 端口）或属性上（数据端口 = 实例属性的值）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    public class NodeInputAttribute : Attribute
    {
        public string Name { get; }
        public string DisplayName { get; }
        public NodePortType PortType { get; }

        /// <param name="name">端口内部标识名（标注在属性上时可省略，默认取属性名）</param>
        public NodeInputAttribute(string name, string displayName, NodePortType portType = NodePortType.Any)
        {
            Name = name;
            DisplayName = displayName;
            PortType = portType;
        }
    }

    /// <summary>
    /// 标记节点的输出端口。可标注在类上或属性上。
    /// 标注在属性上时：执行后框架自动读取属性值作为输出。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    public class NodeOutputAttribute : Attribute
    {
        public string Name { get; }
        public string DisplayName { get; }
        public NodePortType PortType { get; }

        public NodeOutputAttribute(string name, string displayName, NodePortType portType = NodePortType.Any)
        {
            Name = name;
            DisplayName = displayName;
            PortType = portType;
        }
    }

    /// <summary>
    /// 标记节点类中的属性/字段为 Inspector 面板可编辑属性。
    /// Name 自动取成员名，ValueType 从 C# 类型自动推断，DefaultValue 从实例初始值自动读取。
    /// </summary>
    /// <example>
    /// <code>
    /// [NodeProperty(DisplayName = "延时(ms)")]
    /// public int DelayMs { get; set; } = 1000;
    ///
    /// // 最简形式：DisplayName 默认用属性名
    /// [NodeProperty]
    /// public string Name { get; set; } = "";
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class NodePropertyAttribute : Attribute
    {
        /// <summary>面板中显示名称（可选，默认使用属性名）</summary>
        public string? DisplayName { get; set; }

        /// <summary>手动指定值类型（可选，默认从 C# 类型推断：int→Int, double→Double, bool→Bool, string→String, enum→Enum）</summary>
        public NodePropertyValueType? ValueType { get; set; }
    }
}
