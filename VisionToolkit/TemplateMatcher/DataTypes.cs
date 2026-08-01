using System.Drawing;

namespace VisionToolkit.TemplateMatcher;

/// <summary>
/// 匹配器类型枚举
/// </summary>
public enum MatcherType
{
    /// <summary>
    /// 基于归一化互相关(NCC)的金字塔旋转模板匹配
    /// </summary>
    Pattern = 0
}

/// <summary>
/// 匹配器参数配置
/// 调用 Match 前设置，控制匹配行为的各个方面
/// </summary>
public class MatcherParam
{
    /// <summary>
    /// 匹配器类型，目前仅支持 Pattern
    /// </summary>
    public MatcherType MatcherType { get; set; } = MatcherType.Pattern;

    /// <summary>
    /// 最多返回多少个匹配结果，默认 200
    /// </summary>
    public int MaxCount { get; set; } = 200;

    /// <summary>
    /// 匹配分数阈值，范围 [0, 1]
    /// 只有 NCC 分数 ≥ 此值的结果才会被返回，默认 0.5
    /// </summary>
    public double ScoreThreshold { get; set; } = 0.5;

    /// <summary>
    /// 旋转矩形 IoU 阈值，范围 [0, 1],交集和并集面积比阈值
    /// 用于非极大值抑制(NMS)：两个匹配框的重叠比例超过此值，
    /// 只保留分数更高的那个。0 表示不启用 NMS，默认 0
    /// </summary>
    public double IouThreshold { get; set; } = 0.0;

    /// <summary>
    /// 角度搜索范围，单位 度 (°)
    /// 匹配器会在 [-Angle, +Angle] 范围内搜索旋转模板
    /// 设为 0 表示只搜索 0°（不旋转），默认 0
    /// </summary>
    public double Angle { get; set; } = 0;

    /// <summary>
    /// 模板最小面积，单位 像素²
    /// 决定图像金字塔的最高层数：模板面积缩小到此值以下时停止
    /// 减小此值 → 金字塔层数更多 → 更精细但更慢，默认 256
    /// </summary>
    public double MinArea { get; set; } = 256;
}

/// <summary>
/// 单次匹配的结果
/// 包含匹配框的四个角点坐标、中心点坐标、旋转角度和匹配分数
/// </summary>
public class MatchResult
{
    /// <summary>
    /// 匹配框左上角坐标（以原始图像像素为单位）
    /// </summary>
    public PointF LeftTop { get; set; }

    /// <summary>
    /// 匹配框左下角坐标
    /// </summary>
    public PointF LeftBottom { get; set; }

    /// <summary>
    /// 匹配框右上角坐标
    /// </summary>
    public PointF RightTop { get; set; }

    /// <summary>
    /// 匹配框右下角坐标
    /// </summary>
    public PointF RightBottom { get; set; }

    /// <summary>
    /// 匹配框中心点坐标（四个角点的平均值）
    /// </summary>
    public PointF Center { get; set; }

    /// <summary>
    /// 匹配到的旋转角度，单位 度 (°)
    /// -180 ~ 180 范围，正值表示逆时针旋转
    /// </summary>
    public double Angle { get; set; }

    /// <summary>
    /// NCC 归一化互相关匹配分数，范围 [0, 1]
    /// 1 表示完美匹配，越接近 1 表示匹配质量越好
    /// </summary>
    public double Score { get; set; }
}