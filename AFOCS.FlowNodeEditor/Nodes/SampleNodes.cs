using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;

namespace AFOCS.FlowNodeEditor.Nodes
{
    /// <summary>
    /// 入口节点 —— 流程起点，包含共享上下文信息（变量/参数/配置）
    ///   类型3：只有 Execution 输出（无输入），属性成为下游可访问的上下文
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class EntryNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Entry";
        public string DisplayName => "入口";
        public string Category => "流程";

        public IReadOnlyList<INodePortDefinition> InputPorts => [];

        public IReadOnlyList<INodePortDefinition> OutputPorts { get; } =
        [
            new PortDefinition("Out", "输出", NodePortType.Execution),
        ];

        public IReadOnlyList<INodePropertyDefinition> Properties { get; } =
        [
            new PropertyDefinition("Param1", "参数1", NodePropertyValueType.String, ""),
            new PropertyDefinition("Param2", "参数2", NodePropertyValueType.String, ""),
            new PropertyDefinition("Param3", "参数3", NodePropertyValueType.String, ""),
        ];

        public Task<Dictionary<string, object?>> ExecuteAsync(
            Dictionary<string, object?> inputs,
            Dictionary<string, object?> properties,
            Dictionary<string, object?> context)
        {
            // 入口节点的属性值已在外部作为 context 初始值注入
            // 这里可以把处理后的结果也放到 context
            var result = new Dictionary<string, object?>();
            foreach (var kv in properties)
                result[kv.Key] = kv.Value;

            System.Diagnostics.Debug.WriteLine($"[Entry] 流程启动，上下文: {string.Join(", ", result.Select(kv => $"{kv.Key}={kv.Value}"))}");
            return Task.FromResult(result);
        }

        private record PortDefinition(string Name, string DisplayName, NodePortType PortType) : INodePortDefinition;
        private record PropertyDefinition(string Name, string DisplayName, NodePropertyValueType ValueType, object? DefaultValue) : INodePropertyDefinition;
    }

    /// <summary>
    /// 常量节点（类型2：纯数据节点，无 Execution 端口）
    ///   输出一个可配置的值，用于提供测试数据
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class ConstantNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Constant";
        public string DisplayName => "常量";
        public string Category => "基础";

        public IReadOnlyList<INodePortDefinition> InputPorts => [];

        public IReadOnlyList<INodePortDefinition> OutputPorts { get; } =
        [
            new PortDefinition("Value", "值", NodePortType.Any),
        ];

        public IReadOnlyList<INodePropertyDefinition> Properties { get; } =
        [
            new PropertyDefinition("Value", "值", NodePropertyValueType.String, "0"),
        ];

        public Task<Dictionary<string, object?>> ExecuteAsync(
            Dictionary<string, object?> inputs,
            Dictionary<string, object?> properties,
            Dictionary<string, object?> context)
        {
            var rawValue = properties.GetValueOrDefault("Value", "0")?.ToString() ?? "0";

            object? parsed = rawValue;
            if (int.TryParse(rawValue, out var iVal))
                parsed = iVal;
            else if (double.TryParse(rawValue, out var dVal))
                parsed = dVal;
            else if (bool.TryParse(rawValue, out var bVal))
                parsed = bVal;

            return Task.FromResult(new Dictionary<string, object?> { ["Value"] = parsed });
        }

        private record PortDefinition(string Name, string DisplayName, NodePortType PortType) : INodePortDefinition;
        private record PropertyDefinition(string Name, string DisplayName, NodePropertyValueType ValueType, object? DefaultValue) : INodePropertyDefinition;
    }

    /// <summary>
    /// 日志输出节点（类型1：流程节点，有 Execution 输入/输出 + 数据端口）
    ///   打印输入值到 Debug 输出，并透传该值到下游
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class LogNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Log";
        public string DisplayName => "日志输出";
        public string Category => "基础";

        public IReadOnlyList<INodePortDefinition> InputPorts { get; } =
        [
            new PortDefinition("In", "输入", NodePortType.Execution),
            new PortDefinition("Message", "消息", NodePortType.Any),
        ];

        public IReadOnlyList<INodePortDefinition> OutputPorts { get; } =
        [
            new PortDefinition("Out", "输出", NodePortType.Execution),
            new PortDefinition("Output", "输出值", NodePortType.Any),
        ];

        public IReadOnlyList<INodePropertyDefinition> Properties => [];

        public Task<Dictionary<string, object?>> ExecuteAsync(
            Dictionary<string, object?> inputs,
            Dictionary<string, object?> properties,
            Dictionary<string, object?> context)
        {
            var message = inputs.GetValueOrDefault("Message");
            System.Diagnostics.Debug.WriteLine($"[Log] {message}");
            return Task.FromResult(new Dictionary<string, object?>
            {
                ["Output"] = message
            });
        }

        private record PortDefinition(string Name, string DisplayName, NodePortType PortType) : INodePortDefinition;
        private record PropertyDefinition(string Name, string DisplayName, NodePropertyValueType ValueType, object? DefaultValue) : INodePropertyDefinition;
    }

    /// <summary>
    /// 延时节点（类型1：流程节点，只有 Execution 端口，无数据输出）
    ///   暂停指定毫秒数后继续执行线路
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class DelayNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Delay";
        public string DisplayName => "延时";
        public string Category => "基础";

        public IReadOnlyList<INodePortDefinition> InputPorts { get; } =
        [
            new PortDefinition("In", "输入", NodePortType.Execution),
        ];

        public IReadOnlyList<INodePortDefinition> OutputPorts { get; } =
        [
            new PortDefinition("Out", "输出", NodePortType.Execution),
        ];

        public IReadOnlyList<INodePropertyDefinition> Properties { get; } =
        [
            new PropertyDefinition("DelayMs", "延时(ms)", NodePropertyValueType.Int, 1000),
        ];

        public async Task<Dictionary<string, object?>> ExecuteAsync(
            Dictionary<string, object?> inputs,
            Dictionary<string, object?> properties,
            Dictionary<string, object?> context)
        {
            var delayMs = properties.GetValueOrDefault("DelayMs", 1000) is int ms ? ms : 1000;
            await Task.Delay(delayMs);
            return new Dictionary<string, object?>();
        }

        private record PortDefinition(string Name, string DisplayName, NodePortType PortType) : INodePortDefinition;
        private record PropertyDefinition(string Name, string DisplayName, NodePropertyValueType ValueType, object? DefaultValue) : INodePropertyDefinition;
    }

    /// <summary>
    /// 变量赋值节点（类型1：流程节点）
    ///   将输入值写入上下文变量，供下游节点读取
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class SetVariableNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.SetVariable";
        public string DisplayName => "赋值";
        public string Category => "变量";

        public IReadOnlyList<INodePortDefinition> InputPorts { get; } =
        [
            new PortDefinition("In", "输入", NodePortType.Execution),
            new PortDefinition("Value", "值", NodePortType.Any),
        ];

        public IReadOnlyList<INodePortDefinition> OutputPorts { get; } =
        [
            new PortDefinition("Out", "输出", NodePortType.Execution),
        ];

        public IReadOnlyList<INodePropertyDefinition> Properties { get; } =
        [
            new PropertyDefinition("VariableName", "变量名", NodePropertyValueType.String, "myVar"),
        ];

        public Task<Dictionary<string, object?>> ExecuteAsync(
            Dictionary<string, object?> inputs,
            Dictionary<string, object?> properties,
            Dictionary<string, object?> context)
        {
            var varName = properties.GetValueOrDefault("VariableName", "myVar")?.ToString() ?? "myVar";
            var value = inputs.GetValueOrDefault("Value");

            // 写入上下文，下游节点可以通过 context["myVar"] 访问
            context[varName] = value;
            System.Diagnostics.Debug.WriteLine($"[SetVariable] {varName} = {value}");

            return Task.FromResult(new Dictionary<string, object?>());
        }

        private record PortDefinition(string Name, string DisplayName, NodePortType PortType) : INodePortDefinition;
        private record PropertyDefinition(string Name, string DisplayName, NodePropertyValueType ValueType, object? DefaultValue) : INodePropertyDefinition;
    }

    /// <summary>
    /// 读取变量节点（类型2：纯数据节点）
    ///   从上下文读取变量值输出
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class GetVariableNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.GetVariable";
        public string DisplayName => "读取变量";
        public string Category => "变量";

        public IReadOnlyList<INodePortDefinition> InputPorts => [];

        public IReadOnlyList<INodePortDefinition> OutputPorts { get; } =
        [
            new PortDefinition("Value", "值", NodePortType.Any),
        ];

        public IReadOnlyList<INodePropertyDefinition> Properties { get; } =
        [
            new PropertyDefinition("VariableName", "变量名", NodePropertyValueType.String, "myVar"),
        ];

        public Task<Dictionary<string, object?>> ExecuteAsync(
            Dictionary<string, object?> inputs,
            Dictionary<string, object?> properties,
            Dictionary<string, object?> context)
        {
            var varName = properties.GetValueOrDefault("VariableName", "myVar")?.ToString() ?? "myVar";
            context.TryGetValue(varName, out var value);
            return Task.FromResult(new Dictionary<string, object?>
            {
                ["Value"] = value
            });
        }

        private record PortDefinition(string Name, string DisplayName, NodePortType PortType) : INodePortDefinition;
        private record PropertyDefinition(string Name, string DisplayName, NodePropertyValueType ValueType, object? DefaultValue) : INodePropertyDefinition;
    }

    /// <summary>
    /// 加法运算节点（类型2：纯数据节点，无 Execution 端口）
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class AddNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Add";
        public string DisplayName => "加法";
        public string Category => "运算";

        public IReadOnlyList<INodePortDefinition> InputPorts { get; } =
        [
            new PortDefinition("A", "A", NodePortType.Double),
            new PortDefinition("B", "B", NodePortType.Double),
        ];

        public IReadOnlyList<INodePortDefinition> OutputPorts { get; } =
        [
            new PortDefinition("Result", "结果", NodePortType.Double),
        ];

        public IReadOnlyList<INodePropertyDefinition> Properties => [];

        public Task<Dictionary<string, object?>> ExecuteAsync(
            Dictionary<string, object?> inputs,
            Dictionary<string, object?> properties,
            Dictionary<string, object?> context)
        {
            var a = Convert.ToDouble(inputs.GetValueOrDefault("A", 0.0));
            var b = Convert.ToDouble(inputs.GetValueOrDefault("B", 0.0));
            return Task.FromResult(new Dictionary<string, object?> { ["Result"] = a + b });
        }

        private record PortDefinition(string Name, string DisplayName, NodePortType PortType) : INodePortDefinition;
        private record PropertyDefinition(string Name, string DisplayName, NodePropertyValueType ValueType, object? DefaultValue) : INodePropertyDefinition;
    }

    /// <summary>
    /// 减法运算节点（类型2：纯数据节点）
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class SubtractNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Subtract";
        public string DisplayName => "减法";
        public string Category => "运算";

        public IReadOnlyList<INodePortDefinition> InputPorts { get; } =
        [
            new PortDefinition("A", "A", NodePortType.Double),
            new PortDefinition("B", "B", NodePortType.Double),
        ];

        public IReadOnlyList<INodePortDefinition> OutputPorts { get; } =
        [
            new PortDefinition("Result", "结果", NodePortType.Double),
        ];

        public IReadOnlyList<INodePropertyDefinition> Properties => [];

        public Task<Dictionary<string, object?>> ExecuteAsync(
            Dictionary<string, object?> inputs,
            Dictionary<string, object?> properties,
            Dictionary<string, object?> context)
        {
            var a = Convert.ToDouble(inputs.GetValueOrDefault("A", 0.0));
            var b = Convert.ToDouble(inputs.GetValueOrDefault("B", 0.0));
            return Task.FromResult(new Dictionary<string, object?> { ["Result"] = a - b });
        }

        private record PortDefinition(string Name, string DisplayName, NodePortType PortType) : INodePortDefinition;
        private record PropertyDefinition(string Name, string DisplayName, NodePropertyValueType ValueType, object? DefaultValue) : INodePropertyDefinition;
    }

    /// <summary>
    /// 乘法运算节点（类型2：纯数据节点）
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class MultiplyNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Multiply";
        public string DisplayName => "乘法";
        public string Category => "运算";

        public IReadOnlyList<INodePortDefinition> InputPorts { get; } =
        [
            new PortDefinition("A", "A", NodePortType.Double),
            new PortDefinition("B", "B", NodePortType.Double),
        ];

        public IReadOnlyList<INodePortDefinition> OutputPorts { get; } =
        [
            new PortDefinition("Result", "结果", NodePortType.Double),
        ];

        public IReadOnlyList<INodePropertyDefinition> Properties => [];

        public Task<Dictionary<string, object?>> ExecuteAsync(
            Dictionary<string, object?> inputs,
            Dictionary<string, object?> properties,
            Dictionary<string, object?> context)
        {
            var a = Convert.ToDouble(inputs.GetValueOrDefault("A", 0.0));
            var b = Convert.ToDouble(inputs.GetValueOrDefault("B", 0.0));
            return Task.FromResult(new Dictionary<string, object?> { ["Result"] = a * b });
        }

        private record PortDefinition(string Name, string DisplayName, NodePortType PortType) : INodePortDefinition;
        private record PropertyDefinition(string Name, string DisplayName, NodePropertyValueType ValueType, object? DefaultValue) : INodePropertyDefinition;
    }

    /// <summary>
    /// 除法运算节点（类型2：纯数据节点）
    /// </summary>
    [System.ComponentModel.Composition.Export(typeof(INodeDefinition))]
    public class DivideNodeDefinition : INodeDefinition, IExecutableNode
    {
        public string TypeId => "Builtin.Divide";
        public string DisplayName => "除法";
        public string Category => "运算";

        public IReadOnlyList<INodePortDefinition> InputPorts { get; } =
        [
            new PortDefinition("A", "A", NodePortType.Double),
            new PortDefinition("B", "B", NodePortType.Double),
        ];

        public IReadOnlyList<INodePortDefinition> OutputPorts { get; } =
        [
            new PortDefinition("Result", "结果", NodePortType.Double),
        ];

        public IReadOnlyList<INodePropertyDefinition> Properties => [];

        public Task<Dictionary<string, object?>> ExecuteAsync(
            Dictionary<string, object?> inputs,
            Dictionary<string, object?> properties,
            Dictionary<string, object?> context)
        {
            var a = Convert.ToDouble(inputs.GetValueOrDefault("A", 0.0));
            var b = Convert.ToDouble(inputs.GetValueOrDefault("B", 1.0));
            return Task.FromResult(new Dictionary<string, object?> { ["Result"] = b == 0 ? double.NaN : a / b });
        }

        private record PortDefinition(string Name, string DisplayName, NodePortType PortType) : INodePortDefinition;
        private record PropertyDefinition(string Name, string DisplayName, NodePropertyValueType ValueType, object? DefaultValue) : INodePropertyDefinition;
    }
}
