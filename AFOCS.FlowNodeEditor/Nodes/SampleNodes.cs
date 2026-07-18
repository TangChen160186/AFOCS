using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;

namespace AFOCS.FlowNodeEditor.Nodes
{
    [NodeDefinition("Builtin.Entry", "入口", "流程", HasExecutionInput = false, HasExecutionOutput = true)]
    [Export(typeof(INodeDefinition))]
    public class EntryNodeDefinition : INodeDefinition, IExecutableNode
    {
        [DisplayName("参数1")]
        public string Param1 { get; set; } = "";

        [DisplayName("参数2")]
        public string Param2 { get; set; } = "";

        [DisplayName("参数3")]
        public string Param3 { get; set; } = "";

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            var result = new Dictionary<string, object?>
            {
                ["Param1"] = Param1,
                ["Param2"] = Param2,
                ["Param3"] = Param3
            };

            System.Diagnostics.Debug.WriteLine($"[Entry] 流程启动，参数: Param1={Param1}, Param2={Param2}, Param3={Param3}");
            return Task.FromResult(result);
        }
    }

    [NodeDefinition("Builtin.Exit", "出口", "流程", HasExecutionInput = true, HasExecutionOutput = false)]
    [Export(typeof(INodeDefinition))]
    public class ExitNodeDefinition : INodeDefinition, IExecutableNode
    {
        [DisplayName("完成消息")]
        public string Message { get; set; } = "流程执行完成";

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            System.Diagnostics.Debug.WriteLine($"[Exit] {Message}");
            return Task.FromResult(new Dictionary<string, object?>());
        }
    }

    [NodeDefinition("Builtin.Constant", "常量", "基础")]
    [Export(typeof(INodeDefinition))]
    public class ConstantNodeDefinition : INodeDefinition, IExecutableNode
    {
        [DisplayName("值")]
        public string Value { get; set; } = "0";

        [NodePort("Value", "值", NodePortType.Any, false)]
        public object? OutputValue { get; set; }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            var rawValue = Value ?? "0";

            object? parsed = rawValue;
            if (int.TryParse(rawValue, out var iVal))
                parsed = iVal;
            else if (double.TryParse(rawValue, out var dVal))
                parsed = dVal;
            else if (bool.TryParse(rawValue, out var bVal))
                parsed = bVal;

            OutputValue = parsed;
            return Task.FromResult(new Dictionary<string, object?> { ["Value"] = parsed });
        }
    }

    [NodeDefinition("Builtin.Log", "日志输出", "基础")]
    [Export(typeof(INodeDefinition))]
    public class LogNodeDefinition : INodeDefinition, IExecutableNode
    {
        [NodePort("Message", "消息", NodePortType.Any, true)]
        public object? Message { get; set; }

        [NodePort("Output", "输出值", NodePortType.Any, false)]
        public object? Output { get; set; }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            System.Diagnostics.Debug.WriteLine($"[Log] {Message}");
            Output = Message;
            return Task.FromResult(new Dictionary<string, object?>
            {
                ["Output"] = Message
            });
        }
    }

    [NodeDefinition("Builtin.Delay", "延时", "基础")]
    [Export(typeof(INodeDefinition))]
    public class DelayNodeDefinition : INodeDefinition, IExecutableNode
    {
        [DisplayName("延时(ms)")]
        public int DelayMs { get; set; } = 1000;

        public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            await Task.Delay(DelayMs);
            return new Dictionary<string, object?>();
        }
    }

    [NodeDefinition("Builtin.SetVariable", "赋值", "变量")]
    [Export(typeof(INodeDefinition))]
    public class SetVariableNodeDefinition : INodeDefinition, IExecutableNode
    {
        [DisplayName("变量名")]
        public string VariableName { get; set; } = "myVar";

        [NodePort("Value", "值", NodePortType.Any, true)]
        public object? Value { get; set; }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            context[VariableName] = Value;
            System.Diagnostics.Debug.WriteLine($"[SetVariable] {VariableName} = {Value}");
            return Task.FromResult(new Dictionary<string, object?>());
        }
    }

    [NodeDefinition("Builtin.GetVariable", "读取变量", "变量")]
    [Export(typeof(INodeDefinition))]
    public class GetVariableNodeDefinition : INodeDefinition, IExecutableNode
    {
        [DisplayName("变量名")]
        public string VariableName { get; set; } = "myVar";

        [NodePort("Value", "值", NodePortType.Any, false)]
        public object? Value { get; set; }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            context.TryGetValue(VariableName, out var value);
            Value = value;
            return Task.FromResult(new Dictionary<string, object?>
            {
                ["Value"] = value
            });
        }
    }

    [NodeDefinition("Builtin.Add", "加法", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
    [Export(typeof(INodeDefinition))]
    public class AddNodeDefinition : INodeDefinition, IExecutableNode
    {
        [NodePort("A", "A", NodePortType.Double, true)]
        public double A { get; set; }

        [NodePort("B", "B", NodePortType.Double, true)]
        public double B { get; set; }

        [NodePort("Result", "结果", NodePortType.Double, false)]
        public double Result { get; set; }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            Result = A + B;
            return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
        }
    }

    [NodeDefinition("Builtin.Subtract", "减法", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
    [Export(typeof(INodeDefinition))]
    public class SubtractNodeDefinition : INodeDefinition, IExecutableNode
    {
        [NodePort("A", "A", NodePortType.Double, true)]
        public double A { get; set; }

        [NodePort("B", "B", NodePortType.Double, true)]
        public double B { get; set; }

        [NodePort("Result", "结果", NodePortType.Double, false)]
        public double Result { get; set; }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            Result = A - B;
            return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
        }
    }

    [NodeDefinition("Builtin.Multiply", "乘法", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
    [Export(typeof(INodeDefinition))]
    public class MultiplyNodeDefinition : INodeDefinition, IExecutableNode
    {
        [NodePort("A", "A", NodePortType.Double, true)]
        public double A { get; set; }

        [NodePort("B", "B", NodePortType.Double, true)]
        public double B { get; set; }

        [NodePort("Result", "结果", NodePortType.Double, false)]
        public double Result { get; set; }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            Result = A * B;
            return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
        }
    }

    [NodeDefinition("Builtin.Divide", "除法", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
    [Export(typeof(INodeDefinition))]
    public class DivideNodeDefinition : INodeDefinition, IExecutableNode
    {
        [NodePort("A", "A", NodePortType.Double, true)]
        public double A { get; set; }

        [NodePort("B", "B", NodePortType.Double, true)]
        public double B { get; set; }

        [NodePort("Result", "结果", NodePortType.Double, false)]
        public double Result { get; set; }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            Result = B == 0 ? double.NaN : A / B;
            return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
        }
    }
}