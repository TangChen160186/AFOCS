using System.ComponentModel;
using Caliburn.Micro;

namespace AFOCS.VisionEditor.Models;

// ==================== 视觉流程类型 ====================

public enum VisionProcessType
{
    Ncc,
    EdgeFind1,
    EdgeFind2,
    PointFind,
}

// ==================== NCC 模板匹配配置（Halcon ShapeModel） ====================

[DisplayName("NCC 模板匹配")]
public class NccConfig : PropertyChangedBase
{
    [Browsable(false)]
    public override bool IsNotifying { get; set; }
    [Browsable(false)]
    public bool IsEnabled { get; set; } = true;

    // ---- 旋转矩形 ROI（RECTANGLE2） ----

    private double _row = 300;
    [DisplayName("中心行 Row")]
    [Description("旋转矩形中心行坐标")]
    [Category("模板 ROI")]
    public double Row
    {
        get => _row;
        set => Set(ref _row, value);
    }

    private double _column = 400;
    [DisplayName("中心列 Column")]
    [Description("旋转矩形中心列坐标")]
    [Category("模板 ROI")]
    public double Column
    {
        get => _column;
        set => Set(ref _column, value);
    }

    private double _phi = 0;
    [DisplayName("旋转角 Phi")]
    [Description("旋转矩形角度（弧度）")]
    [Category("模板 ROI")]
    public double Phi
    {
        get => _phi;
        set => Set(ref _phi, value);
    }

    private double _length1 = 300;
    [DisplayName("半长 Length1")]
    [Description("旋转矩形半长（px）")]
    [Category("模板 ROI")]
    public double Length1
    {
        get => _length1;
        set => Set(ref _length1, value);
    }

    private double _length2 = 200;
    [DisplayName("半宽 Length2")]
    [Description("旋转矩形半宽（px）")]
    [Category("模板 ROI")]
    public double Length2
    {
        get => _length2;
        set => Set(ref _length2, value);
    }

    /// <summary>拖拽回调用：直接设置字段，不触发 PropertyChanged（避免 PropertyGrid 双向绑定干扰 Halcon 鼠标捕获）</summary>
    internal void UpdateFromDrag(double row, double column, double phi, double length1, double length2)
    {
        _row = row;
        _column = column;
        _phi = phi;
        _length1 = length1;
        _length2 = length2;
    }

    /// <summary>拖拽结束后一次性刷新所有 ROI 属性通知</summary>
    internal void NotifyDragEnd()
    {
        NotifyOfPropertyChange(nameof(Row));
        NotifyOfPropertyChange(nameof(Column));
        NotifyOfPropertyChange(nameof(Phi));
        NotifyOfPropertyChange(nameof(Length1));
        NotifyOfPropertyChange(nameof(Length2));
    }

    // ---- 匹配参数 ----

    [DisplayName("最小匹配分数")]
    [Description("FindShapeModel 最低匹配分数（0~1）")]
    [Category("匹配参数")]
    public double MinScore { get; set; } = 0.5;

    [Browsable(false)]
    public string ModelPath { get; set; } = string.Empty;

    // ---- 执行结果 ----

    [DisplayName("匹配中心 X")]
    [Description("匹配到的模板中心 Column 坐标（像素）")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultX { get; set; }

    [DisplayName("匹配中心 Y")]
    [Description("匹配到的模板中心 Row 坐标（像素）")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultY { get; set; }

    [DisplayName("匹配角度")]
    [Description("匹配到的旋转角度（度）")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultAngle { get; set; }

    [DisplayName("匹配分数")]
    [Description("ShapeModel 匹配分数（0~1）")]
    [Category("执行结果")]
    [ReadOnly(true)]
    public double ResultScore { get; set; }
}

// ==================== 找边配置 ====================

[DisplayName("找边")]
public class EdgeFindConfig : PropertyChangedBase
{
    [Browsable(false)]
    public override bool IsNotifying { get; set; }
    [Browsable(false)]
    public bool IsEnabled { get; set; } = true;

    // ---- 测量线 ROI（Halcon 计量模型线段） ----

    private double _row1 = 100;
    [DisplayName("起点行 Row1")]
    [Description("测量线段起点行坐标")]
    [Category("测量线 ROI")]
    public double Row1
    {
        get => _row1;
        set => Set(ref _row1, value);
    }

    private double _col1 = 100;
    [DisplayName("起点列 Col1")]
    [Description("测量线段起点列坐标")]
    [Category("测量线 ROI")]
    public double Col1
    {
        get => _col1;
        set => Set(ref _col1, value);
    }

    private double _row2 = 100;
    [DisplayName("终点行 Row2")]
    [Description("测量线段终点行坐标")]
    [Category("测量线 ROI")]
    public double Row2
    {
        get => _row2;
        set => Set(ref _row2, value);
    }

    private double _col2 = 200;
    [DisplayName("终点列 Col2")]
    [Description("测量线段终点列坐标")]
    [Category("测量线 ROI")]
    public double Col2
    {
        get => _col2;
        set => Set(ref _col2, value);
    }

    /// <summary>拖拽回调用：直接设置字段，不触发 PropertyChanged（避免 PropertyGrid 双向绑定干扰 Halcon 鼠标捕获）</summary>
    internal void UpdateFromDrag(double row1, double col1, double row2, double col2)
    {
        _row1 = row1;
        _col1 = col1;
        _row2 = row2;
        _col2 = col2;
    }

    /// <summary>拖拽结束后一次性刷新所有 ROI 属性通知</summary>
    internal void NotifyDragEnd()
    {
        NotifyOfPropertyChange(nameof(Row1));
        NotifyOfPropertyChange(nameof(Col1));
        NotifyOfPropertyChange(nameof(Row2));
        NotifyOfPropertyChange(nameof(Col2));
    }

    // ---- 计量模型参数 ----

    [DisplayName("测量半长1")]
    [Description("垂直于测量线方向的检测区域半长（px）")]
    [Category("计量参数")]
    public double MeasureLength1 { get; set; } = 20;

    [DisplayName("测量半长2")]
    [Description("沿测量线方向的检测区域半长（px）")]
    [Category("计量参数")]
    public double MeasureLength2 { get; set; } = 20;

    [DisplayName("平滑系数 Sigma")]
    [Description("高斯平滑系数")]
    [Category("计量参数")]
    public double MeasureSigma { get; set; } = 1;

    [DisplayName("边缘阈值")]
    [Description("边缘对比度阈值")]
    [Category("计量参数")]
    public double MeasureThreshold { get; set; } = 20;

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
