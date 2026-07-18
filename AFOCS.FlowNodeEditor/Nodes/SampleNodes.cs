using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;

namespace AFOCS.FlowNodeEditor.Nodes
{
    /// <summary>
    /// 入口节点 —— 流程起点
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    [NodeOutput("Out", "输出", NodePortType.Execution)]
    public class EntryNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Entry";
        public string DisplayName => "入口";
        public string Category => "流程";

        [NodeProperty(DisplayName = "参数1")]
        public string Param1 { get; set; } = "";

        [NodeProperty(DisplayName = "参数2")]
        public string Param2 { get; set; } = "";

        [NodeProperty(DisplayName = "参数3")]
        public string Param3 { get; set; } = "";

        public Task ExecuteAsync(Dictionary<string, object?> context)
        {
            context["Param1"] = Param1;
            context["Param2"] = Param2;
            context["Param3"] = Param3;

            System.Diagnostics.Debug.WriteLine($"[Entry] 流程启动，Param1={Param1}, Param2={Param2}, Param3={Param3}");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 常量节点
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class ConstantNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Constant";
        public string DisplayName => "常量";
        public string Category => "基础";

        [NodeProperty(DisplayName = "值")]
        [NodeOutput("Value", "值", NodePortType.Any)]
        public object? Value { get; set; } = 0;

        public Task ExecuteAsync(Dictionary<string, object?> context)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 日志输出节点
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    [NodeInput("In", "输入", NodePortType.Execution)]
    [NodeOutput("Out", "输出", NodePortType.Execution)]
    public class LogNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Log";
        public string DisplayName => "日志输出";
        public string Category => "基础";

        [NodeInput("Message", "消息", NodePortType.Any)]
        public object? Message { get; set; }

        [NodeOutput("Output", "输出值", NodePortType.Any)]
        public object? Output { get; set; }

        public Task ExecuteAsync(Dictionary<string, object?> context)
        {
            System.Diagnostics.Debug.WriteLine($"[Log] {Message}");
            Output = Message;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 延时节点
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    [NodeInput("In", "输入", NodePortType.Execution)]
    [NodeOutput("Out", "输出", NodePortType.Execution)]
    public class DelayNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Delay";
        public string DisplayName => "延时";
        public string Category => "基础";

        [NodeProperty(DisplayName = "延时(ms)")]
        public int DelayMs { get; set; } = 1000;

        public async Task ExecuteAsync(Dictionary<string, object?> context)
        {
            await Task.Delay(DelayMs);
        }
    }

    /// <summary>
    /// 变量赋值节点
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    [NodeInput("In", "输入", NodePortType.Execution)]
    [NodeOutput("Out", "输出", NodePortType.Execution)]
    public class SetVariableNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.SetVariable";
        public string DisplayName => "赋值";
        public string Category => "变量";

        [NodeProperty(DisplayName = "变量名")]
        public string VariableName { get; set; } = "myVar";

        [NodeInput("Value", "值", NodePortType.Any)]
        public object? Value { get; set; }

        public Task ExecuteAsync(Dictionary<string, object?> context)
        {
            context[VariableName] = Value;
            System.Diagnostics.Debug.WriteLine($"[SetVariable] {VariableName} = {Value}");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 读取变量节点
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class GetVariableNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.GetVariable";
        public string DisplayName => "读取变量";
        public string Category => "变量";

        [NodeProperty(DisplayName = "变量名")]
        public string VariableName { get; set; } = "myVar";

        [NodeOutput("Value", "值", NodePortType.Any)]
        public object? Value { get; set; }

        public Task ExecuteAsync(Dictionary<string, object?> context)
        {
            context.TryGetValue(VariableName, out var val);
            Value = val;
            return Task.CompletedTask;
        }
    }

    // ========== 运算节点 ==========

    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class AddNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Add";
        public string DisplayName => "加法";
        public string Category => "运算";

        [NodeInput("A", "A", NodePortType.Double)]
        public double A { get; set; }

        [NodeInput("B", "B", NodePortType.Double)]
        public double B { get; set; }

        [NodeOutput("Result", "结果", NodePortType.Double)]
        public double Result { get; set; }

        public Task ExecuteAsync(Dictionary<string, object?> context)
        {
            Result = A + B;
            return Task.CompletedTask;
        }
    }

    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class SubtractNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Subtract";
        public string DisplayName => "减法";
        public string Category => "运算";

        [NodeInput("A", "A", NodePortType.Double)]
        public double A { get; set; }

        [NodeInput("B", "B", NodePortType.Double)]
        public double B { get; set; }

        [NodeOutput("Result", "结果", NodePortType.Double)]
        public double Result { get; set; }

        public Task ExecuteAsync(Dictionary<string, object?> context)
        {
            Result = A - B;
            return Task.CompletedTask;
        }
    }

    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class MultiplyNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Multiply";
        public string DisplayName => "乘法";
        public string Category => "运算";

        [NodeInput("A", "A", NodePortType.Double)]
        public double A { get; set; }

        [NodeInput("B", "B", NodePortType.Double)]
        public double B { get; set; }

        [NodeOutput("Result", "结果", NodePortType.Double)]
        public double Result { get; set; }

        public Task ExecuteAsync(Dictionary<string, object?> context)
        {
            Result = A * B;
            return Task.CompletedTask;
        }
    }

    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class DivideNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Divide";
        public string DisplayName => "除法";
        public string Category => "运算";

        [NodeInput("A", "A", NodePortType.Double)]
        public double A { get; set; }

        [NodeInput("B", "B", NodePortType.Double)]
        public double B { get; set; }

        [NodeOutput("Result", "结果", NodePortType.Double)]
        public double Result { get; set; }

        public Task ExecuteAsync(Dictionary<string, object?> context)
        {
            Result = B == 0 ? double.NaN : A / B;
            return Task.CompletedTask;
        }
    }
}
