using System;

namespace AFOCS.FlowNodeEditor.Models
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class NodeDefinitionAttribute : Attribute
    {
        public string TypeId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public string? IconSource { get; set; }
        public bool HasExecutionInput { get; set; } = true;
        public bool HasExecutionOutput { get; set; } = true;

        public NodeDefinitionAttribute(string typeId, string displayName, string category)
        {
            TypeId = typeId;
            DisplayName = displayName;
            Category = category;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class NodePortAttribute : Attribute
    {
        public string Name { get; }
        public string DisplayName { get; }
        public NodePortType PortType { get; }
        public bool IsInput { get; }
        public bool AllowPropertyEdit { get; set; } = true;

        public NodePortAttribute(string name, string displayName, NodePortType portType, bool isInput)
        {
            Name = name;
            DisplayName = displayName;
            PortType = portType;
            IsInput = isInput;
        }
    }
}