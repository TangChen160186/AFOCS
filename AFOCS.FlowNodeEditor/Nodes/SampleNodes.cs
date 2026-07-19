using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;

namespace AFOCS.FlowNodeEditor.Nodes
{
    [NodeDefinition("Builtin.Entry", "入口", "流程", HasExecutionInput = false, HasExecutionOutput = true)]
    [Export(typeof(INodeDefinition))]
    public class EntryNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        private int _priority = 0;
        [DisplayName("优先级")]
        public int Priority { get => _priority; set => Set(ref _priority, value); }

        private string _param1 = "";
        [DisplayName("参数1")]
        public string Param1 { get => _param1; set => Set(ref _param1, value); }

        private string _param2 = "";
        [DisplayName("参数2")]
        public string Param2 { get => _param2; set => Set(ref _param2, value); }

        private string _param3 = "";
        [DisplayName("参数3")]
        public string Param3 { get => _param3; set => Set(ref _param3, value); }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            var result = new Dictionary<string, object?>
            {
                ["Param1"] = Param1,
                ["Param2"] = Param2,
                ["Param3"] = Param3
            };

            System.Diagnostics.Debug.WriteLine($"[Entry] 流程启动(优先级={Priority})，参数: Param1={Param1}, Param2={Param2}, Param3={Param3}");
            return Task.FromResult(result);
        }
    }

    [NodeDefinition("Builtin.Exit", "出口", "流程", HasExecutionInput = true, HasExecutionOutput = false)]
    [Export(typeof(INodeDefinition))]
    public class ExitNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        private string _message = "流程执行完成";
        [DisplayName("完成消息")]
        public string Message { get => _message; set => Set(ref _message, value); }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            System.Diagnostics.Debug.WriteLine($"[Exit] {Message}");
            return Task.FromResult(new Dictionary<string, object?>());
        }
    }

    [NodeDefinition("Builtin.Constant", "常量", "基础", HasExecutionInput = false, HasExecutionOutput = false)]
    [Export(typeof(INodeDefinition))]
    public class ConstantNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        private string _value = "0";
        [DisplayName("值")]
        public string Value 
        { 
            get => _value; 
            set 
            { 
                if (Set(ref _value, value))
                {
                    UpdateOutput();
                }
            } 
        }

        private object? _outputValue;
        [NodePort("Value", "值", NodePortType.Any, false)]
        public object? OutputValue { get => _outputValue; set => Set(ref _outputValue, value); }

        private void UpdateOutput()
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
        }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            UpdateOutput();
            return Task.FromResult(new Dictionary<string, object?> { ["Value"] = OutputValue });
        }
    }

    [NodeDefinition("Builtin.Log", "日志输出", "基础", HasExecutionInput = false, HasExecutionOutput = false)]
    [Export(typeof(INodeDefinition))]
    public class LogNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        private object? _message;
        [NodePort("Message", "消息", NodePortType.Any, true)]
        public object? Message { get => _message; set => Set(ref _message, value); }

        private object? _output;
        [NodePort("Output", "输出值", NodePortType.Any, false)]
        public object? Output { get => _output; set => Set(ref _output, value); }

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
    public class DelayNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        private int _delayMs = 1000;
        [DisplayName("延时(ms)")]
        public int DelayMs { get => _delayMs; set => Set(ref _delayMs, value); }

        public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            await Task.Delay(DelayMs);
            return new Dictionary<string, object?>();
        }
    }

    [NodeDefinition("Builtin.SetVariable", "赋值", "变量")]
    [Export(typeof(INodeDefinition))]
    public class SetVariableNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        private string _variableName = "myVar";
        [DisplayName("变量名")]
        public string VariableName { get => _variableName; set => Set(ref _variableName, value); }

        private object? _value;
        [NodePort("Value", "值", NodePortType.Any, true)]
        public object? Value { get => _value; set => Set(ref _value, value); }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            context[VariableName] = Value;
            System.Diagnostics.Debug.WriteLine($"[SetVariable] {VariableName} = {Value}");
            return Task.FromResult(new Dictionary<string, object?>());
        }
    }

    [NodeDefinition("Builtin.GetVariable", "读取变量", "变量")]
    [Export(typeof(INodeDefinition))]
    public class GetVariableNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        private string _variableName = "myVar";
        [DisplayName("变量名")]
        public string VariableName { get => _variableName; set => Set(ref _variableName, value); }

        private object? _value;
        [NodePort("Value", "值", NodePortType.Any, false)]
        public object? Value { get => _value; set => Set(ref _value, value); }

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
    public class AddNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        private double _a;
        [NodePort("A", "A", NodePortType.Double, true)]
        public double A { get => _a; set => Set(ref _a, value); }

        private double _b;
        [NodePort("B", "B", NodePortType.Double, true)]
        public double B { get => _b; set => Set(ref _b, value); }

        private double _result;
        [NodePort("Result", "结果", NodePortType.Double, false)]
        public double Result { get => _result; set => Set(ref _result, value); }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            Result = A + B;
            return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
        }
    }

    [NodeDefinition("Builtin.Subtract", "减法", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
    [Export(typeof(INodeDefinition))]
    public class SubtractNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        private double _a;
        [NodePort("A", "A", NodePortType.Double, true)]
        public double A { get => _a; set => Set(ref _a, value); }

        private double _b;
        [NodePort("B", "B", NodePortType.Double, true)]
        public double B { get => _b; set => Set(ref _b, value); }

        private double _result;
        [NodePort("Result", "结果", NodePortType.Double, false)]
        public double Result { get => _result; set => Set(ref _result, value); }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            Result = A - B;
            return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
        }
    }

    [NodeDefinition("Builtin.Multiply", "乘法", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
    [Export(typeof(INodeDefinition))]
    public class MultiplyNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        private double _a;
        [NodePort("A", "A", NodePortType.Double, true)]
        public double A { get => _a; set => Set(ref _a, value); }

        private double _b;
        [NodePort("B", "B", NodePortType.Double, true)]
        public double B { get => _b; set => Set(ref _b, value); }

        private double _result;
        [NodePort("Result", "结果", NodePortType.Double, false)]
        public double Result { get => _result; set => Set(ref _result, value); }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            Result = A * B;
            return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
        }
    }

    [NodeDefinition("Builtin.Divide", "除法", "运算", HasExecutionInput = false, HasExecutionOutput = false)]
    [Export(typeof(INodeDefinition))]
    public class DivideNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        private double _a;
        [NodePort("A", "A", NodePortType.Double, true)]
        public double A { get => _a; set => Set(ref _a, value); }

        private double _b;
        [NodePort("B", "B", NodePortType.Double, true)]
        public double B { get => _b; set => Set(ref _b, value); }

        private double _result;
        [NodePort("Result", "结果", NodePortType.Double, false)]
        public double Result { get => _result; set => Set(ref _result, value); }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            Result = B == 0 ? double.NaN : A / B;
            return Task.FromResult(new Dictionary<string, object?> { ["Result"] = Result });
        }
    }
}