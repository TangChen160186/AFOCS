using System.Reflection;
using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.Services
{
    /// <summary>
    /// 节点定义反射扫描器。
    /// - 端口：类级 [NodeInput]/[NodeOutput] + 属性级 [NodeInput]/[NodeOutput] 合并发现
    /// - 属性：成员级 [NodeProperty]，ValueType 从 C# 类型推断，DefaultValue 从实例读取
    /// </summary>
    public static class NodeDefinitionScanner
    {
        /// <summary>扫描所有输入端口（类级 Attribute + 属性级 Attribute 合并）</summary>
        public static IReadOnlyList<INodePortDefinition> ScanInputPorts(Type nodeType)
        {
            var result = new List<INodePortDefinition>();

            // 类级 [NodeInput]
            foreach (var attr in nodeType.GetCustomAttributes<NodeInputAttribute>())
                result.Add(new ScannedPortDefinition(attr.Name, attr.DisplayName, attr.PortType));

            // 属性级 [NodeInput]
            foreach (var prop in nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<NodeInputAttribute>();
                if (attr == null) continue;
                var name = string.IsNullOrWhiteSpace(attr.Name) ? prop.Name : attr.Name;
                result.Add(new ScannedPortDefinition(name, attr.DisplayName, attr.PortType));
            }

            return result;
        }

        /// <summary>扫描所有输出端口（类级 + 属性级合并）</summary>
        public static IReadOnlyList<INodePortDefinition> ScanOutputPorts(Type nodeType)
        {
            var result = new List<INodePortDefinition>();

            foreach (var attr in nodeType.GetCustomAttributes<NodeOutputAttribute>())
                result.Add(new ScannedPortDefinition(attr.Name, attr.DisplayName, attr.PortType));

            foreach (var prop in nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<NodeOutputAttribute>();
                if (attr == null) continue;
                var name = string.IsNullOrWhiteSpace(attr.Name) ? prop.Name : attr.Name;
                result.Add(new ScannedPortDefinition(name, attr.DisplayName, attr.PortType));
            }

            return result;
        }

        /// <summary>
        /// 扫描标注了 [NodeProperty] 的公开属性/字段。
        /// ValueType 从 C# 类型推断，DefaultValue 从 instance 读取。
        /// </summary>
        public static IReadOnlyList<INodePropertyDefinition> ScanProperties(Type nodeType, object? instance)
        {
            var result = new List<INodePropertyDefinition>();

            foreach (var prop in nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<NodePropertyAttribute>();
                if (attr == null) continue;

                var valueType = attr.ValueType ?? InferValueType(prop.PropertyType);
                var defaultVal = instance != null ? prop.GetValue(instance) : null;

                result.Add(new ScannedPropertyDefinition(
                    Name: prop.Name,
                    DisplayName: attr.DisplayName ?? prop.Name,
                    ValueType: valueType,
                    DefaultValue: defaultVal));
            }

            foreach (var field in nodeType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = field.GetCustomAttribute<NodePropertyAttribute>();
                if (attr == null) continue;

                var valueType = attr.ValueType ?? InferValueType(field.FieldType);
                var defaultVal = instance != null ? field.GetValue(instance) : null;

                result.Add(new ScannedPropertyDefinition(
                    Name: field.Name,
                    DisplayName: attr.DisplayName ?? field.Name,
                    ValueType: valueType,
                    DefaultValue: defaultVal));
            }

            return result;
        }

        /// <summary>从 C# 类型推断属性值类型</summary>
        private static NodePropertyValueType InferValueType(Type type)
        {
            if (type.IsEnum) return NodePropertyValueType.Enum;

            var t = Nullable.GetUnderlyingType(type) ?? type;

            if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte))
                return NodePropertyValueType.Int;

            if (t == typeof(double) || t == typeof(float) || t == typeof(decimal))
                return NodePropertyValueType.Double;

            if (t == typeof(bool))
                return NodePropertyValueType.Bool;

            return NodePropertyValueType.String;
        }

        /// <summary>获取属性级输出端口名到 PropertyInfo 的映射（FlowExecutor 用）</summary>
        public static Dictionary<string, PropertyInfo?> GetOutputPropertyMap(Type nodeType)
        {
            var map = new Dictionary<string, PropertyInfo?>();
            foreach (var prop in nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<NodeOutputAttribute>();
                if (attr == null) continue;
                var name = string.IsNullOrWhiteSpace(attr.Name) ? prop.Name : attr.Name;
                map[name] = prop;
            }
            return map;
        }

        /// <summary>获取属性级输入端口名到 PropertyInfo 的映射（FlowExecutor 用）</summary>
        public static Dictionary<string, PropertyInfo?> GetInputPropertyMap(Type nodeType)
        {
            var map = new Dictionary<string, PropertyInfo?>();
            foreach (var prop in nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<NodeInputAttribute>();
                if (attr == null) continue;
                var name = string.IsNullOrWhiteSpace(attr.Name) ? prop.Name : attr.Name;
                map[name] = prop;
            }
            return map;
        }

        // ========== internal records ==========

        private record ScannedPortDefinition(string Name, string DisplayName, NodePortType PortType)
            : INodePortDefinition;

        private record ScannedPropertyDefinition(
            string Name, string DisplayName, NodePropertyValueType ValueType, object? DefaultValue)
            : INodePropertyDefinition;
    }
}
