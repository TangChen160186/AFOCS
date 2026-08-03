using System.Windows;
using AFOCS.FlowNodeEditor.Models;
using Caliburn.Micro;

namespace AFOCS.FlowNodeEditor.ViewModels;

/// <summary>
/// 连接器 ViewModel —— Nodify 通过 Anchor 属性获取连接点位置
/// </summary>
public class ConnectorViewModel(NodeViewModel parent, INodePortDefinition portDef, bool isInput)
    : PropertyChangedBase
{
    public string Name { get; } = portDef.Name;
    public string DisplayName { get; } = portDef.DisplayName;
    public NodePortType PortType { get; } = portDef.PortType;

    public bool IsInput { get; } = isInput;

    public bool IsConnected
    {
        get;
        set => Set(ref field, value);
    }

    /// <summary>Nodify 自动设置的连接点位置（画布坐标）</summary>
    public Point Anchor
    {
        get;
        set => Set(ref field, value);
    }

    /// <summary>从属的节点 ViewModel</summary>
    public NodeViewModel Parent { get; } = parent;

    public Guid ParentInstanceId => Parent.InstanceId;
}