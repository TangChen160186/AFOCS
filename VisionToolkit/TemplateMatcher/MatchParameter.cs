using System.Drawing;
using Emgu.CV.Structure;

namespace VisionToolkit.TemplateMatcher;

/// <summary>
/// 单个候选匹配的中间参数，贯穿整个匹配流水线
/// 从顶层粗匹配产生，逐层精细后最终转换为 MatchResult 输出
/// </summary>
public class MatchParameter
{
    /// <summary>
    /// 匹配位置坐标（在对应金字塔层的图像坐标系中）
    /// </summary>
    public PointF Pt;

    /// <summary>
    /// NCC 归一化互相关匹配分数，范围 [0, 1]
    /// 1 表示完美匹配
    /// </summary>
    public double DMatchScore;

    /// <summary>
    /// 匹配到的旋转角度，单位 度 (°)
    /// 正值表示逆时针旋转
    /// </summary>
    public double DMatchAngle;

    /// <summary>
    /// 角度搜索区间的起始值，单位 度 (°)
    /// 仅在顶层多角度搜索时有意义
    /// </summary>
    public double DAngleStart;

    /// <summary>
    /// 角度搜索区间的结束值，单位 度 (°)
    /// 仅在顶层多角度搜索时有意义
    /// </summary>
    public double DAngleEnd;

    /// <summary>
    /// 旋转矩形框，四个角点坐标构成的旋转矩形
    /// 用于后续的旋转矩形 NMS 去重
    /// </summary>
    public RotatedRect RectR;

    /// <summary>
    /// 标记位，NMS 时标记该候选是否被抑制（删除）
    /// true 表示该候选将被移除
    /// </summary>
    public bool BDelete;

    /// <summary>
    /// 3×3 邻域匹配分数矩阵
    /// 在像素级最佳位置周围采样 9 个点的分数，
    /// 用于亚像素二次曲面拟合
    /// VecResult[dx+1, dy+1] 存储 (pt.X+dx, pt.Y+dy) 处的分数
    /// </summary>
    public double[,] VecResult = new double[3, 3];

    /// <summary>
    /// 标记位，该匹配位置是否落在 ROI 图像边界上
    /// 边界上的点无法进行亚像素估计（缺少邻域数据）
    /// </summary>
    public bool BPosOnBorder;

    /// <summary>
    /// 默认构造函数，初始化删除标记和边界标记为 false
    /// </summary>
    public MatchParameter()
    {
        BDelete = false;
        BPosOnBorder = false;
    }

    /// <summary>
    /// 带参数构造函数，用于从匹配结果直接创建候选
    /// </summary>
    /// <param name="ptMinMax">匹配位置坐标</param>
    /// <param name="dScore">NCC 匹配分数</param>
    /// <param name="dAngle">旋转角度，单位 度</param>
    public MatchParameter(PointF ptMinMax, double dScore, double dAngle)
    {
        Pt = ptMinMax;
        DMatchScore = dScore;
        DMatchAngle = dAngle;
        BDelete = false;
        BPosOnBorder = false;
        VecResult = new double[3, 3];
    }
}