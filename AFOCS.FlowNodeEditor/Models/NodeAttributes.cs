namespace AFOCS.FlowNodeEditor.Models;

[AttributeUsage(AttributeTargets.Class)]
public class NodeDefinitionAttribute(string typeId, string displayName, string category) : Attribute
{
    public string TypeId { get; } = typeId;
    public string DisplayName { get; } = displayName;
    public string Category { get; } = category;
    public string? IconSource { get; set; }
    public bool HasExecutionInput { get; set; } = true;
    public bool HasExecutionOutput { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Property)]
public class NodePortAttribute(string name, string displayName, NodePortType portType, bool isInput)
    : Attribute
{
    public string Name { get; } = name;
    public string DisplayName { get; } = displayName;
    public NodePortType PortType { get; } = portType;
    public bool IsInput { get; } = isInput;
    public bool AllowPropertyEdit { get; set; } = true;
}