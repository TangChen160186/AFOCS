using Emgu.CV;
using Emgu.CV.Structure;

namespace VisionToolkit.TemplateMatcher;

/// <summary>
/// 模板金字塔数据，存储模板图像在各层金字塔上的预处理信息
/// 由 LearnPattern 填充，供后续各层匹配使用
/// </summary>
internal class TemplData
{
    /// <summary>
    /// 模板图像的高斯金字塔
    /// VecPyramid[0] = 原始尺寸模板（底层）
    /// VecPyramid[N] = 最顶层（最小尺寸）
    /// </summary>
    public List<Mat> VecPyramid = new List<Mat>();

    /// <summary>
    /// 每层金字塔模板的像素均值
    /// NCC 归一化时用于减去窗口均值
    /// </summary>
    public List<MCvScalar> VecTemplMean = new List<MCvScalar>();

    /// <summary>
    /// 每层金字塔模板的标准差 × sqrt(像素数)
    /// NCC 归一化公式的分母组成部分
    /// </summary>
    public List<double> VecTemplNorm = new List<double>();

    /// <summary>
    /// 每层金字塔模板面积的倒数 1/(W×H)
    /// 用于快速计算窗口均值（积分图差 × invArea = 均值）
    /// </summary>
    public List<double> VecInvArea = new List<double>();

    /// <summary>
    /// 标记位，该层模板是否为常量图像（无纹理）
    /// 若模板所有像素值相同（标准差≈0），则 NCC 分母为 0，
    /// 直接返回分数 1，避免除零错误
    /// </summary>
    public List<bool> VecResultEqual1 = new List<bool>();

    /// <summary>
    /// 模板是否已完成学习（已调用 LearnPattern）
    /// Match 前必须为 true
    /// </summary>
    public bool BIsPatternLearned = false;

    /// <summary>
    /// 模板的边界填充颜色
    /// 0 = 黑色填充，255 = 白色填充
    /// 由模板平均灰度决定：平均 < 128 用白色(255)，否则用黑色(0)
    /// 旋转模板时超出画布的区域用此颜色填充
    /// </summary>
    public int IBorderColor = 0;

    /// <summary>
    /// 清空所有数据，释放金字塔中的 Mat 资源
    /// </summary>
    public void Clear()
    {
        foreach (var m in VecPyramid) m?.Dispose();
        VecPyramid.Clear();
        VecTemplMean.Clear();
        VecTemplNorm.Clear();
        VecInvArea.Clear();
        VecResultEqual1.Clear();
        BIsPatternLearned = false;
    }

    /// <summary>
    /// 确保各列表容量至少为 size，不足则填充默认值
    /// size 等于金字塔层数
    /// </summary>
    public void Resize(int size)
    {
        while (VecTemplMean.Count < size) VecTemplMean.Add(new MCvScalar());
        while (VecTemplNorm.Count < size) VecTemplNorm.Add(0);
        while (VecInvArea.Count < size) VecInvArea.Add(1);
        while (VecResultEqual1.Count < size) VecResultEqual1.Add(false);
    }
}