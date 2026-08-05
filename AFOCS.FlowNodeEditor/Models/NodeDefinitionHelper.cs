using System.Reflection;
using System.Text.Json;
using AFOCS.FlowNodeEditor.Services;
using Caliburn.Micro;

namespace AFOCS.FlowNodeEditor.Models;

public static class NodeDefinitionHelper
{
    public static NodeDefinitionAttribute? GetDefinitionAttribute(INodeDefinition definition)
    {
        return definition.GetType().GetCustomAttribute<NodeDefinitionAttribute>();
    }

    public static string GetTypeId(INodeDefinition definition)
    {
        var attr = GetDefinitionAttribute(definition);
        return attr?.TypeId ?? definition.GetType().FullName ?? definition.GetType().Name;
    }

    public static string GetDisplayName(INodeDefinition definition)
    {
        var attr = GetDefinitionAttribute(definition);
        return attr?.DisplayName ?? definition.GetType().Name;
    }

    public static string GetCategory(INodeDefinition definition)
    {
        var attr = GetDefinitionAttribute(definition);
        return attr?.Category ?? "未分类";
    }

    public static Uri? GetIconSource(INodeDefinition definition)
    {
        var attr = GetDefinitionAttribute(definition);
        return attr?.IconSource != null ? new Uri(attr.IconSource) : null;
    }

    public static bool HasExecutionInput(INodeDefinition definition)
    {
        var attr = GetDefinitionAttribute(definition);
        return attr?.HasExecutionInput ?? typeof(IExecutableNode).IsAssignableFrom(definition.GetType());
    }

    public static bool HasExecutionOutput(INodeDefinition definition)
    {
        var attr = GetDefinitionAttribute(definition);
        return attr?.HasExecutionOutput ?? typeof(IExecutableNode).IsAssignableFrom(definition.GetType());
    }

    public static bool HasExecutionFlow(INodeDefinition definition)
    {
        return HasExecutionInput(definition) || HasExecutionOutput(definition);
    }

    public static IReadOnlyList<INodePortDefinition> GetInputPorts(INodeDefinition definition)
    {
        var ports = new List<INodePortDefinition>();
        var type = definition.GetType();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var portAttr = prop.GetCustomAttribute<NodePortAttribute>();
            if (portAttr != null && portAttr.IsInput)
            {
                ports.Add(new RuntimePortDefinition(portAttr.Name, portAttr.DisplayName, portAttr.PortType));
            }
        }

        if (HasExecutionInput(definition))
        {
            ports.Add(new RuntimePortDefinition("In", "输入", NodePortType.Execution));
        }

        return ports;
    }

    public static IReadOnlyList<INodePortDefinition> GetOutputPorts(INodeDefinition definition)
    {
        var ports = new List<INodePortDefinition>();
        var type = definition.GetType();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var portAttr = prop.GetCustomAttribute<NodePortAttribute>();
            if (portAttr != null && !portAttr.IsInput)
            {
                ports.Add(new RuntimePortDefinition(portAttr.Name, portAttr.DisplayName, portAttr.PortType));
            }
        }

        if (HasExecutionOutput(definition))
        {
            ports.Add(new RuntimePortDefinition("Out", "输出", NodePortType.Execution));
        }

        return ports;
    }

    public static bool IsInputPortProperty(INodeDefinition definition, string propertyName)
    {
        var prop = definition.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null) return false;
        var portAttr = prop.GetCustomAttribute<NodePortAttribute>();
        return portAttr != null && portAttr.IsInput;
    }

    public static bool IsOutputPortProperty(INodeDefinition definition, string propertyName)
    {
        var prop = definition.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null) return false;
        var portAttr = prop.GetCustomAttribute<NodePortAttribute>();
        return portAttr != null && !portAttr.IsInput;
    }

    public static bool AllowPropertyEdit(INodeDefinition definition, string propertyName)
    {
        var prop = definition.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null) return true;
        var portAttr = prop.GetCustomAttribute<NodePortAttribute>();
        return portAttr == null || portAttr.AllowPropertyEdit;
    }

    /// <summary>
    /// 将节点定义的序列化 JSON 按属性声明类型还原到已有实例（实例由容器创建，保留依赖注入）。
    /// 类型转换完全交给 System.Text.Json，新增任意可序列化类型无需修改框架。
    /// </summary>
    public static void ApplySerialized(INodeDefinition definition, string? json)
    {
        if (string.IsNullOrEmpty(json)) return;

        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var propInfo = definition.GetType()
                .GetProperty(prop.Name, BindingFlags.Public | BindingFlags.Instance);
            if (propInfo is not { CanWrite: true }) continue;

            var value = JsonSerializer.Deserialize(prop.Value.GetRawText(), propInfo.PropertyType);
            propInfo.SetValue(definition, value);
        }
    }

    public static INodeDefinition Clone(INodeDefinition source)
    {
        var type = source.GetType();

        var clone = IoC.GetInstance(type, null);

        //var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        //foreach (var field in fields)
        //{
        //    field.SetValue(clone, field.GetValue(source));
        //}

        //var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        //foreach (var prop in props)
        //{
        //    if (!prop.CanRead || !prop.CanWrite || prop.GetIndexParameters().Length > 0) continue;
        //    prop.SetValue(clone, prop.GetValue(source));
        //}

        return (INodeDefinition)clone;
    }

    private record RuntimePortDefinition(string Name, string DisplayName, NodePortType PortType) : INodePortDefinition;
}