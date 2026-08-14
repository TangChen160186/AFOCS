using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.FlowNodeEditor.Nodes;

/// <summary>
/// 正弦节点：输入弧度，输出 sin 值。
/// </summary>
[NodeDefinition("Builtin.Sin", "正弦", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[Export(typeof(INodeDefinition))]
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class SinNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    [NodePort("Value", "弧度", NodePortType.Double, true)]
    [Category("输入")]
    public double Value
    {
        get;
        set => Set(ref field, value);
    }

    [Browsable(false)]
    [NodePort("Result", "结果", NodePortType.Double, false)]
    [Category("输出")]
    public double Result
    {
        get;
        set => Set(ref field, value);
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        Result = Math.Sin(Value);
        return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
    }
}

/// <summary>
/// 余弦节点：输入弧度，输出 cos 值。
/// </summary>
[NodeDefinition("Builtin.Cos", "余弦", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[Export(typeof(INodeDefinition))]
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class CosNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    [NodePort("Value", "弧度", NodePortType.Double, true)]
    [Category("输入")]
    public double Value
    {
        get;
        set => Set(ref field, value);
    }

    [Browsable(false)]
    [NodePort("Result", "结果", NodePortType.Double, false)]
    [Category("输出")]
    public double Result
    {
        get;
        set => Set(ref field, value);
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        Result = Math.Cos(Value);
        return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
    }
}

/// <summary>
/// 正切节点：输入弧度，输出 tan 值。
/// </summary>
[NodeDefinition("Builtin.Tan", "正切", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[Export(typeof(INodeDefinition))]
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class TanNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    [NodePort("Value", "弧度", NodePortType.Double, true)]
    [Category("输入")]
    public double Value
    {
        get;
        set => Set(ref field, value);
    }

    [Browsable(false)]
    [NodePort("Result", "结果", NodePortType.Double, false)]
    [Category("输出")]
    public double Result
    {
        get;
        set => Set(ref field, value);
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        Result = Math.Tan(Value);
        return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
    }
}

/// <summary>
/// 反正弦节点：输入 [-1, 1] 的值，输出弧度。
/// </summary>
[NodeDefinition("Builtin.Asin", "反正弦", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[Export(typeof(INodeDefinition))]
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class AsinNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    [NodePort("Value", "值", NodePortType.Double, true)]
    [Category("输入")]
    public double Value
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
        Result = Math.Asin(Value);
        return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
    }
}

/// <summary>
/// 反余弦节点：输入 [-1, 1] 的值，输出弧度。
/// </summary>
[NodeDefinition("Builtin.Acos", "反余弦", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[Export(typeof(INodeDefinition))]
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class AcosNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    [NodePort("Value", "值", NodePortType.Double, true)]
    [Category("输入")]
    public double Value
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
        Result = Math.Acos(Value);
        return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
    }
}

/// <summary>
/// 反正切节点：输入值，输出弧度。
/// </summary>
[NodeDefinition("Builtin.Atan", "反正切", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[Export(typeof(INodeDefinition))]
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class AtanNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    [NodePort("Value", "值", NodePortType.Double, true)]
    [Category("输入")]
    public double Value
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
        Result = Math.Atan(Value);
        return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
    }
}
