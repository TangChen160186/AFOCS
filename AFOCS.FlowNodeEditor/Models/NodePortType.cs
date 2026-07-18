namespace AFOCS.FlowNodeEditor.Models
{
    /// <summary>
    /// 端口数据类型
    /// </summary>
    public enum NodePortType
    {
        /// <summary>任意类型</summary>
        Any,
        /// <summary>布尔值</summary>
        Bool,
        /// <summary>整数</summary>
        Int,
        /// <summary>浮点数</summary>
        Double,
        /// <summary>字符串</summary>
        String,
        /// <summary>执行流（控制流）</summary>
        Execution,
        /// <summary>图像数据</summary>
        Image,
        /// <summary>对象引用</summary>
        Object,
    }

    /// <summary>
    /// 属性值类型
    /// </summary>
    public enum NodePropertyValueType
    {
        String,
        Int,
        Double,
        Bool,
        Enum,
    }
}
