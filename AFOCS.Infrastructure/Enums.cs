using System.ComponentModel;

namespace AFOCS.Infrastructure;

public enum WorkPos
{
    [Description("左工位")]
    Left,
    [Description("右工位")]
    Right,
}



/// <summary>
/// 压力传感器通道（X/Y/Z）
/// </summary>
public enum PressureChannel
{
    /// <summary>X 通道（子索引 1）</summary>
    X = 0,
    /// <summary>Y 通道（子索引 2）</summary>
    Y = 1,
    /// <summary>Z 通道（子索引 3）</summary>
    Z = 2,
}