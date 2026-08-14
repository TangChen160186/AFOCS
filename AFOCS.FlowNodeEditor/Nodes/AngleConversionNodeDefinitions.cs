using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.FlowNodeEditor.Nodes;

/// <summary>
/// 角度转弧度节点：输入角度（度），输出弧度。
/// </summary>
[NodeDefinition("Builtin.DegToRad", "角度转弧度", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[Export(typeof(INodeDefinition))]
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class DegToRadNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    [NodePort("Degrees", "角度", NodePortType.Double, true)]
    [Category("输入")]
    public double Degrees
    {
        get;
        set => Set(ref field, value);
    }

    [Browsable(false)]
    [NodePort("Result", "弧度", NodePortType.Double, false)]
    [Category("输出")]
    public double Result
    {
        get;
        set => Set(ref field, value);
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        Result = Degrees * Math.PI / 180.0;
        return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
    }
}

/// <summary>
/// 弧度转角度节点：输入弧度，输出角度（度）。
/// </summary>
[NodeDefinition("Builtin.RadToDeg", "弧度转角度", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[Export(typeof(INodeDefinition))]
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class RadToDegNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    [NodePort("Radians", "弧度", NodePortType.Double, true)]
    [Category("输入")]
    public double Radians
    {
        get;
        set => Set(ref field, value);
    }

    [Browsable(false)]
    [NodePort("Result", "角度", NodePortType.Double, false)]
    [Category("输出")]
    public double Result
    {
        get;
        set => Set(ref field, value);
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        Result = Radians * 180.0 / Math.PI;
        return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
    }
}
