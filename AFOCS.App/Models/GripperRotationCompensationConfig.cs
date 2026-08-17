using System.ComponentModel;
using AFOCS.Infrastructure;

namespace AFOCS.App.Models;

/// <summary>
/// 夹爪旋转补偿配置：夹爪旋转中心偏离自身中心，绕某轴旋转后需按
/// 「直线轴初始角度 + 旋转半径」计算补偿偏移。配置以直线轴为单位（X/Y/Z 各一个初始角度和半径）：
///   绕 X 轴旋转 → 用 Y、Z 轴的配置补偿（影响 YZ 平面）
///   绕 Y 轴旋转 → 用 X、Z 轴的配置补偿（影响 XZ 平面）
///   绕 Z 轴旋转 → 用 X、Y 轴的配置补偿（影响 XY 平面）
/// 单位：角度=度、半径=um。
/// </summary>
[ConfigPath("夹爪/旋转补偿")]
public class GripperRotationCompensationConfig
{
    [DisplayName("X 轴")]
    [Description("直线轴 X 的初始角度与旋转半径")]
    public AxisRotationCompensation X { get; set; } = new();

    [DisplayName("Y 轴")]
    [Description("直线轴 Y 的初始角度与旋转半径")]
    public AxisRotationCompensation Y { get; set; } = new();

    [DisplayName("Z 轴")]
    [Description("直线轴 Z 的初始角度与旋转半径")]
    public AxisRotationCompensation Z { get; set; } = new();
}

/// <summary>单个直线轴的旋转补偿参数（角度=度、半径=um）</summary>
public class AxisRotationCompensation
{
    [DisplayName("初始角度(度)")]
    [Description("无旋转参考位时该轴方向与水平面的夹角")]
    public double InitialAngle { get; set; }

    [DisplayName("旋转半径(um)")]
    [Description("该轴方向上旋转中心到夹爪中心的距离")]
    public double Radius { get; set; }
}
