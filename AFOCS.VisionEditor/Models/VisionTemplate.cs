using System.ComponentModel;
using System.Text.Json.Serialization;
using Caliburn.Micro;
using VisionToolkit.TemplateMatcher;

namespace AFOCS.VisionEditor.Models;

// ==================== ROI 数据（内部序列化用） ====================

public class RoiData
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Angle { get; set; }

    [JsonIgnore]
    public bool IsValid => Width > 0 && Height > 0;

    public static RoiData Empty => new();
}

// ==================== 视觉流程类型 ====================

public enum VisionProcessType
{
    Ncc,
    EdgeFind1,
    EdgeFind2,
    PointFind,
}

// ==================== NCC 模板匹配配置 ====================

[DisplayName("NCC 模板匹配")]
public class NccConfig : PropertyChangedBase
{
    [Browsable(false)]
    public override bool IsNotifying { get; set; }
    [Browsable(false)]
    public bool IsEnabled { get; set; } = true;

    // ---- 搜索 ROI（序列化用） ----
    [Browsable(false)]
    public RoiData SearchRoi { get; set; } = new();

    [DisplayName("X")]
    [Category("搜索 ROI")]
    public double SearchRoiX
    {
        get => SearchRoi.X;
        set { if (SetValue(SearchRoi.X, value, v => SearchRoi.X = v)) NotifyOfPropertyChange(); }
    }

    [DisplayName("Y")]
    [Category("搜索 ROI")]
    public double SearchRoiY
    {
        get => SearchRoi.Y;
        set { if (SetValue(SearchRoi.Y, value, v => SearchRoi.Y = v)) NotifyOfPropertyChange(); }
    }

    [DisplayName("宽")]
    [Category("搜索 ROI")]
    public double SearchRoiWidth
    {
        get => SearchRoi.Width;
        set { if (SetValue(SearchRoi.Width, value, v => SearchRoi.Width = v)) NotifyOfPropertyChange(); }
    }

    [DisplayName("高")]
    [Category("搜索 ROI")]
    public double SearchRoiHeight
    {
        get => SearchRoi.Height;
        set { if (SetValue(SearchRoi.Height, value, v => SearchRoi.Height = v)) NotifyOfPropertyChange(); }
    }

    [DisplayName("角度")]
    [Category("搜索 ROI")]
    public double SearchRoiAngle
    {
        get => SearchRoi.Angle;
        set { if (SetValue(SearchRoi.Angle, value, v => SearchRoi.Angle = v)) NotifyOfPropertyChange(); }
    }

    // ---- 模板 ROI（序列化用） ----
    [Browsable(false)]
    public RoiData TemplateRoi { get; set; } = new();

    [DisplayName("X")]
    [Category("模板 ROI")]
    public double TemplateRoiX
    {
        get => TemplateRoi.X;
        set { if (SetValue(TemplateRoi.X, value, v => TemplateRoi.X = v)) NotifyOfPropertyChange(); }
    }

    [DisplayName("Y")]
    [Category("模板 ROI")]
    public double TemplateRoiY
    {
        get => TemplateRoi.Y;
        set { if (SetValue(TemplateRoi.Y, value, v => TemplateRoi.Y = v)) NotifyOfPropertyChange(); }
    }

    [DisplayName("宽")]
    [Category("模板 ROI")]
    public double TemplateRoiWidth
    {
        get => TemplateRoi.Width;
        set { if (SetValue(TemplateRoi.Width, value, v => TemplateRoi.Width = v)) NotifyOfPropertyChange(); }
    }

    [DisplayName("高")]
    [Category("模板 ROI")]
    public double TemplateRoiHeight
    {
        get => TemplateRoi.Height;
        set { if (SetValue(TemplateRoi.Height, value, v => TemplateRoi.Height = v)) NotifyOfPropertyChange(); }
    }

    [DisplayName("角度")]
    [Category("模板 ROI")]
    public double TemplateRoiAngle
    {
        get => TemplateRoi.Angle;
        set { if (SetValue(TemplateRoi.Angle, value, v => TemplateRoi.Angle = v)) NotifyOfPropertyChange(); }
    }

    // ---- 匹配参数 ----

    [DisplayName("分数阈值")]
    [Description("NCC 匹配分数下限（0~1）")]
    [Category("匹配参数")]
    public double ScoreThreshold { get; set; } = 0.5;

    [DisplayName("搜索角度")]
    [Description("角度搜索范围（度），0=不旋转")]
    [Category("匹配参数")]
    public double SearchAngle { get; set; } = 0;

    [DisplayName("最大匹配数")]
    [Description("最多返回多少个匹配结果")]
    [Category("匹配参数")]
    [ReadOnly(true)]
    public int MaxCount { get; set; } = 1;

    [DisplayName("最小面积")]
    [Description("模板最小面积（px²），决定金字塔层数")]
    [Category("匹配参数")]
    public double MinArea { get; set; } = 256;

    [DisplayName("IoU 阈值")]
    [Description("NMS 去重重叠阈值，0=不启用")]
    [Category("匹配参数")]
    [ReadOnly(true)]
    public double IouThreshold { get; set; } = 0.0;

    // ---- 执行结果 ----

    [DisplayName("匹配中心 X")]
    [Description("匹配到的模板中心 X 坐标（像素）")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultX { get; set; }

    [DisplayName("匹配中心 Y")]
    [Description("匹配到的模板中心 Y 坐标（像素）")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultY { get; set; }

    [DisplayName("匹配角度")]
    [Description("匹配到的旋转角度（度）")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultAngle { get; set; }

    [DisplayName("匹配分数")]
    [Description("NCC 归一化互相关分数（0~1）")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultScore { get; set; }

    [JsonIgnore]
    [Browsable(false)]
    public MatchResult? Result { get; set; }

    private static bool SetValue(double current, double newValue, Action<double> apply)
    {
        if (Math.Abs(current - newValue) < 0.001) return false;
        apply(newValue);
        return true;
    }
}

// ==================== 找边配置 ====================

[DisplayName("找边")]
public class EdgeFindConfig : PropertyChangedBase
{
    [Browsable(false)]
    public override bool IsNotifying { get; set; }
    [Browsable(false)]
    public bool IsEnabled { get; set; } = true;

    // ---- 搜索 ROI（序列化用） ----
    [Browsable(false)]
    public RoiData SearchRoi { get; set; } = new();

    [DisplayName("X")]
    [Category("搜索 ROI")]
    public double SearchRoiX
    {
        get => SearchRoi.X;
        set { if (SetValue(SearchRoi.X, value, v => SearchRoi.X = v)) NotifyOfPropertyChange(); }
    }

    [DisplayName("Y")]
    [Category("搜索 ROI")]
    public double SearchRoiY
    {
        get => SearchRoi.Y;
        set { if (SetValue(SearchRoi.Y, value, v => SearchRoi.Y = v)) NotifyOfPropertyChange(); }
    }

    [DisplayName("宽")]
    [Category("搜索 ROI")]
    public double SearchRoiWidth
    {
        get => SearchRoi.Width;
        set { if (SetValue(SearchRoi.Width, value, v => SearchRoi.Width = v)) NotifyOfPropertyChange(); }
    }

    [DisplayName("高")]
    [Category("搜索 ROI")]
    public double SearchRoiHeight
    {
        get => SearchRoi.Height;
        set { if (SetValue(SearchRoi.Height, value, v => SearchRoi.Height = v)) NotifyOfPropertyChange(); }
    }

    [DisplayName("角度")]
    [Description("ROI 旋转角（度）")]
    [Category("搜索 ROI")]
    public double EdgeAngleDeg
    {
        get => SearchRoi.Angle;
        set { if (SetValue(SearchRoi.Angle, value, v => SearchRoi.Angle = v)) NotifyOfPropertyChange(); }
    }

    // ---- 找边参数 ----

    [DisplayName("边缘方向角")]
    [Description("要找的边缘方向（度），0°=横边，90°=竖边")]
    [Category("找边参数")]
    public double EdgeDirectionDeg { get; set; } = 90;

    [DisplayName("卡尺数量")]
    [Description("沿边方向等距放置的卡尺数量")]
    [Category("找边参数")]
    public int CaliperCount { get; set; } = 20;

    [DisplayName("卡尺宽度")]
    [Description("每条扫描线的投影宽度（px）")]
    [Category("找边参数")]
    public double CaliperWidth { get; set; } = 5;

    [DisplayName("搜索半长")]
    [Description("从中心向两侧搜索的半长（px）")]
    [Category("找边参数")]
    public double SearchHalf { get; set; } = 40;

    [DisplayName("内点阈值")]
    [Description("RANSAC 内点判定距离（px）")]
    [Category("找边参数")]
    public double InlierThreshold { get; set; } = 0.8;

    // ---- 执行结果 ----

    [DisplayName("线段起点 X")]
    [Description("找到的边缘线段起点 X 坐标")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultStartX { get; set; }

    [DisplayName("线段起点 Y")]
    [Description("找到的边缘线段起点 Y 坐标")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultStartY { get; set; }

    [DisplayName("线段终点 X")]
    [Description("找到的边缘线段终点 X 坐标")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultEndX { get; set; }

    [DisplayName("线段终点 Y")]
    [Description("找到的边缘线段终点 Y 坐标")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultEndY { get; set; }

    [DisplayName("线段角度")]
    [Description("找到的边缘线段角度（度）")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultAngleDeg { get; set; }

    private static bool SetValue(double current, double newValue, Action<double> apply)
    {
        if (Math.Abs(current - newValue) < 0.001) return false;
        apply(newValue);
        return true;
    }
}

// ==================== 找点配置 ====================

[DisplayName("找点")]
public class PointFindConfig : PropertyChangedBase
{
    [Browsable(false)]
    public override bool IsNotifying { get; set; }
    [Browsable(false)]
    public bool IsEnabled { get; set; } = true;

    [DisplayName("交点 X")]
    [Description("Edge1 与 Edge2 的交点 X 坐标")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultX { get; set; }

    [DisplayName("交点 Y")]
    [Description("Edge1 与 Edge2 的交点 Y 坐标")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultY { get; set; }
}

// ==================== 视觉模板（顶层序列化容器） ====================

public class VisionTemplate
{
    public string Name { get; set; } = string.Empty;

    [Browsable(false)]
    public string ImagePath { get; set; } = string.Empty;

    public NccConfig Ncc { get; set; } = new();

    public EdgeFindConfig EdgeFind1 { get; set; } = new();

    public EdgeFindConfig EdgeFind2 { get; set; } = new();

    public PointFindConfig PointFind { get; set; } = new();
}
