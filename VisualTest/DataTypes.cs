using System.Drawing;

namespace VisualTest;

// 匹配器类型
public enum MatcherType
{
    Pattern = 0
}

// 匹配器参数
public class MatcherParam
{
    public MatcherType MatcherType { get; set; } = MatcherType.Pattern;
    public int MaxCount { get; set; } = 200;
    public double ScoreThreshold { get; set; } = 0.5;
    public double IouThreshold { get; set; } = 0.0;
    public double Angle { get; set; } = 0;
    public double MinArea { get; set; } = 256;
}

// 匹配结果
public class MatchResult
{
    public PointF LeftTop { get; set; }
    public PointF LeftBottom { get; set; }
    public PointF RightTop { get; set; }
    public PointF RightBottom { get; set; }
    public PointF Center { get; set; }
    public double Angle { get; set; }
    public double Score { get; set; }
}