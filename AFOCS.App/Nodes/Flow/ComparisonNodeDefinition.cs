using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Flow;

public enum ComparisonOperator
{
    [Description("大于")]
    GreaterThan,

    [Description("大于等于")]
    GreaterThanOrEqual,

    [Description("小于")]
    LessThan,

    [Description("小于等于")]
    LessThanOrEqual,

    [Description("等于")]
    Equal,

    [Description("不等于")]
    NotEqual,
}

/// <summary>
/// 值判断节点：比较输入值与目标值，输出布尔结果（供条件判断节点使用）。
/// 输入值与目标值均可从其它节点连线获得，也可在属性面板直接编辑（未连线时使用面板值）。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.Compare", "值判断", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class ComparisonNodeDefinition(ILogger logger) : NodeDefinitionBase, IExecutableNode
{
    [Browsable(false)]
    [NodePort("Value", "输入值", NodePortType.Double, true)]
    [Category("输入")]
    public double Value
    {
        get;
        set => Set(ref field, value);
    }

    [DisplayName("比较方式")]
    [ItemsSource(typeof(ComparisonOperatorItemsSource))]
    [Category("配置")]
    public ComparisonOperator Operator
    {
        get;
        set => Set(ref field, value);
    } = ComparisonOperator.GreaterThan;
    [Browsable(false)]
    [NodePort("TargetValue", "目标值", NodePortType.Double, true)]
    [Category("输入")]
    public double TargetValue
    {
        get;
        set => Set(ref field, value);
    }

    [Browsable(false)]
    [NodePort("Result", "结果", NodePortType.Bool, false)]
    [Category("输出")]
    public bool Result
    {
        get;
        set => Set(ref field, value);
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        Result = Operator switch
        {
            ComparisonOperator.GreaterThan => Value > TargetValue,
            ComparisonOperator.GreaterThanOrEqual => Value >= TargetValue,
            ComparisonOperator.LessThan => Value < TargetValue,
            ComparisonOperator.LessThanOrEqual => Value <= TargetValue,
            ComparisonOperator.Equal => Value.Equals(TargetValue),
            ComparisonOperator.NotEqual => !Value.Equals(TargetValue),
            _ => false,
        };

        logger.Information("值判断: {Value} {Operator} {Target} = {Result}",
            Value, Operator, TargetValue, Result);

        return Task.FromResult(new Dictionary<string, object?>
        {
            ["Result"] = Result,
        });
    }
}