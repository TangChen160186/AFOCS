namespace AFOCS.FlowNodeEditor.Models
{
    public interface INodePortDefinition
    {
        string Name { get; }
        string DisplayName { get; }
        NodePortType PortType { get; }
    }

    public interface INodeDefinition
    {
    }
}