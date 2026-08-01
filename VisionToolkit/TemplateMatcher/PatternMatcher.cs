using System.Drawing;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace VisionToolkit.TemplateMatcher;

/// <summary>
/// 匹配器内部常量
/// </summary>
internal static class Const
{
    /// <summary>浮点比较容差，用于角度特殊值（0/90/180/270）的精确判断</summary>
    public const double VisionTolerance = 0.0000001;
    /// <summary>角度转弧度系数：D2R = PI / 180</summary>
    public const double D2R = Math.PI / 180.0;
    /// <summary>弧度转角度系数：R2D = 180 / PI</summary>
    public const double R2D = 180.0 / Math.PI;
    /// <summary>顶层匹配时额外多取的候选数，确保逐层精化时有足够冗余</summary>
    public const int MatchCandidateNum = 5;
}

/// <summary>
/// 基于 NCC 归一化互相关的金字塔旋转模板匹配器
/// 支持多目标、任意角度搜索，输出亚像素精度结果
/// </summary>
public class PatternMatcher : IDisposable
{
    /// <summary>模板金字塔预处理数据</summary>
    private TemplData _templData = new TemplData();
    /// <summary>用户配置的匹配参数</summary>
    private MatcherParam _param;
    /// <summary>原始模板图像（灰度单通道）</summary>
    private Mat _templateImage;
    /// <summary>匹配器是否已初始化成功</summary>
    private bool _isInited;

    /// <summary>调试模式开关（预留）</summary>
    public bool DebugMode { get; set; }
    /// <summary>是否在最底层启用亚像素精度估计，默认 true</summary>
    public bool SubPixel { get; set; } = true;
    /// <summary>是否在金字塔第 1 层停止（不降到第 0 层），默认 false</summary>
    public bool StopLayer1 { get; set; }
    /// <summary>是否打印各阶段耗时（预留）</summary>
    public bool MetricsTime { get; set; }

    /// <summary>
    /// 构造匹配器，传入匹配参数
    /// </summary>
    public PatternMatcher(MatcherParam param)
    {
        _param = param;
        _templateImage = new Mat();
        _isInited = true;
    }

    /// <summary>匹配器是否已初始化</summary>
    public bool IsInited => _isInited;

    /// <summary>释放模板图像和金字塔数据</summary>
    public void Dispose()
    {
        _templateImage?.Dispose();
        _templData?.Clear();
    }

    // ==================== 模板学习 ====================

    /// <summary>
    /// 设置模板图像并执行学习（构建金字塔 + 计算各层统计量）
    /// 必须在 Match 之前调用
    /// </summary>
    /// <param name="templateImage">模板图像，必须是灰度单通道</param>
    /// <returns>学习成功返回 true</returns>
    public bool SetTemplate(Mat templateImage)
    {
        if (templateImage == null || templateImage.IsEmpty) return false;
        if (templateImage.NumberOfChannels > 1) return false;

        _templData.BIsPatternLearned = false;
        _templateImage?.Dispose();
        _templateImage = templateImage.Clone();
        LearnPattern(_templData, _templateImage, _param.MinArea);
        return true;
    }

    /// <summary>
    /// 计算金字塔顶层索引
    /// 从原始尺寸开始，每层面积缩小为 1/4（宽高各减半），
    /// 直到面积小于等于 minReduceArea 时停止
    /// </summary>
    /// <param name="matTempl">模板图像</param>
    /// <param name="iMinDstLength">最小边长（sqrt(MinArea)）</param>
    /// <returns>金字塔顶层编号（0 为原始层）</returns>
    private static int GetTopLayer(Mat matTempl, int iMinDstLength)
    {
        int iTopLayer = 0;
        double iMinReduceArea = iMinDstLength * (double)iMinDstLength;
        double iArea = matTempl.Width * (double)matTempl.Height;
        while (iArea > iMinReduceArea) { iArea /= 4.0; iTopLayer++; }
        return iTopLayer;
    }

    /// <summary>
    /// 模板学习：构建高斯金字塔并计算每层的统计量
    /// 为后续 NCC 归一化匹配预计算均值、标准差范数、面积倒数等
    /// </summary>
    /// <param name="templData">输出的模板金字塔数据</param>
    /// <param name="matDst">模板图像</param>
    /// <param name="minReduceArea">最小面积阈值，决定金字塔层数</param>
    private static void LearnPattern(TemplData templData, Mat matDst, double minReduceArea)
    {
        templData.Clear();
        int iTopLayer = GetTopLayer(matDst, (int)Math.Sqrt(minReduceArea));

        using (VectorOfMat pyramid = new VectorOfMat())
        {
            CvInvoke.BuildPyramid(matDst, pyramid, iTopLayer);
            for (int i = 0; i < pyramid.Size; i++)
                templData.VecPyramid.Add(pyramid[i].Clone());
        }

        var meanScalar = CvInvoke.Mean(matDst);
        templData.IBorderColor = meanScalar.V0 < 128 ? 255 : 0;

        int iSize = templData.VecPyramid.Count;
        templData.Resize(iSize);

        for (int i = 0; i < iSize; i++)
        {
            double invArea = 1.0 / (templData.VecPyramid[i].Width * (double)templData.VecPyramid[i].Height);

            using (Mat meanMat = new Mat())
            using (Mat stddevMat = new Mat())
            {
                CvInvoke.MeanStdDev(templData.VecPyramid[i], meanMat, stddevMat);
                double[] meanVals = new double[meanMat.Rows];
                double[] stdVals = new double[stddevMat.Rows];
                Marshal.Copy(meanMat.DataPointer, meanVals, 0, meanVals.Length);
                Marshal.Copy(stddevMat.DataPointer, stdVals, 0, stdVals.Length);

                double templNorm = 0, templSum2 = 0;
                for (int c = 0; c < meanVals.Length; c++)
                {
                    templNorm += stdVals[c] * stdVals[c];
                    templSum2 += stdVals[c] * stdVals[c] + meanVals[c] * meanVals[c];
                }

                if (templNorm < double.Epsilon) templData.VecResultEqual1[i] = true;

                templSum2 /= invArea;
                templNorm = Math.Sqrt(templNorm) / Math.Sqrt(invArea);

                templData.VecInvArea[i] = invArea;
                templData.VecTemplMean[i] = new MCvScalar(meanVals[0]);
                templData.VecTemplNorm[i] = templNorm;
            }
        }
        templData.BIsPatternLearned = true;
    }

    // ==================== 几何辅助函数 ====================

    /// <summary>
    /// 点绕中心旋转（OpenCV 图像坐标系，Y 轴向下）
    /// 通过 Y 轴翻转模拟标准数学坐标系旋转后再翻回
    /// </summary>
    /// <param name="ptInput">输入点坐标</param>
    /// <param name="ptOrg">旋转中心</param>
    /// <param name="dAngle">旋转角度（弧度），正值逆时针</param>
    /// <returns>旋转后的点坐标</returns>
    private static PointF PtRotatePt2f(PointF ptInput, PointF ptOrg, double dAngle)
    {
        double dHeight = ptOrg.Y * 2;
        double dY1 = dHeight - ptInput.Y;
        double dY2 = dHeight - ptOrg.Y;
        double dX = (ptInput.X - ptOrg.X) * Math.Cos(dAngle) - (dY1 - ptOrg.Y) * Math.Sin(dAngle) + ptOrg.X;
        double dY = (ptInput.X - ptOrg.X) * Math.Sin(dAngle) + (dY1 - ptOrg.Y) * Math.Cos(dAngle) + dY2;
        return new PointF((float)dX, (float)(-dY + dHeight));
    }

    /// <summary>
    /// 计算图像旋转后所需的最小包围画布尺寸
    /// 旋转图像后四个角点可能超出原图范围，需要更大的画布来容纳
    /// </summary>
    /// <param name="sizeSrc">原始图像尺寸</param>
    /// <param name="sizeDst">目标比较尺寸（用于校验）</param>
    /// <param name="dRAngle">旋转角度（度）</param>
    /// <returns>旋转后所需的画布尺寸</returns>
    private static Size GetBestRotationSize(Size sizeSrc, Size sizeDst, double dRAngle)
    {
        double dR = dRAngle * Const.D2R;
        PointF ptCenter = new PointF((sizeSrc.Width - 1) / 2f, (sizeSrc.Height - 1) / 2f);

        var pts = new[] {
            PtRotatePt2f(new PointF(0, 0), ptCenter, dR),
            PtRotatePt2f(new PointF(0, sizeSrc.Height - 1), ptCenter, dR),
            PtRotatePt2f(new PointF(sizeSrc.Width - 1, sizeSrc.Height - 1), ptCenter, dR),
            PtRotatePt2f(new PointF(sizeSrc.Width - 1, 0), ptCenter, dR)
        };

        float fTopY = pts.Max(p => p.Y), fBottomY = pts.Min(p => p.Y);
        float fRightX = pts.Max(p => p.X), fLeftX = pts.Min(p => p.X);

        double dAngle = dRAngle;
        if (dAngle > 360) dAngle -= 360; else if (dAngle < 0) dAngle += 360;

        if (Math.Abs(Math.Abs(dAngle) - 90) < Const.VisionTolerance ||
            Math.Abs(Math.Abs(dAngle) - 270) < Const.VisionTolerance)
            return new Size(sizeSrc.Height, sizeSrc.Width);
        if (Math.Abs(dAngle) < Const.VisionTolerance ||
            Math.Abs(Math.Abs(dAngle) - 180) < Const.VisionTolerance)
            return sizeSrc;

        if (dAngle > 90 && dAngle < 180) dAngle -= 90;
        else if (dAngle > 180 && dAngle < 270) dAngle -= 180;
        else if (dAngle > 270 && dAngle < 360) dAngle -= 270;

        float fH1 = sizeDst.Width * (float)(Math.Sin(dAngle * Const.D2R) * Math.Cos(dAngle * Const.D2R));
        float fH2 = sizeDst.Height * (float)(Math.Sin(dAngle * Const.D2R) * Math.Cos(dAngle * Const.D2R));

        int iHalfH = (int)Math.Ceiling(fTopY - ptCenter.Y - fH1);
        int iHalfW = (int)Math.Ceiling(fRightX - ptCenter.X - fH2);
        Size sizeRet = new Size(iHalfW * 2, iHalfH * 2);

        bool bWrong = (sizeDst.Width < sizeRet.Width && sizeDst.Height > sizeRet.Height)
                      || (sizeDst.Width > sizeRet.Width && sizeDst.Height < sizeRet.Height)
                      || (sizeDst.Width * sizeDst.Height > sizeRet.Width * sizeRet.Height);
        if (bWrong)
            sizeRet = new Size((int)(fRightX - fLeftX + 0.5), (int)(fTopY - fBottomY + 0.5));
        return sizeRet;
    }

    // ==================== NCC 匹配核心 ====================

    /// <summary>
    /// NCC 归一化互相关的分母归一化
    /// 将 CCORR 的原始结果通过积分图逐像素计算窗口方差，
    /// 除以分母完成归一化，得到最终 NCC 分数
    /// NCC = Num / Den，其中 Den = sqrt(Var_src) × stddev_templ × sqrt(N)
    /// </summary>
    /// <param name="matSrc">场景图像（积分图的源）</param>
    /// <param name="pTemplData">模板金字塔数据</param>
    /// <param name="matResult">CCORR 结果 Mat，输入输出，被修改为 NCC 分数</param>
    /// <param name="iLayer">当前金字塔层索引</param>
    private static void CCOEFFDenominator(Mat matSrc, TemplData pTemplData, Mat matResult, int iLayer)
    {
        if (pTemplData.VecResultEqual1[iLayer])
        {
            matResult.SetTo(new MCvScalar(1));
            return;
        }

        using (Mat sum = new Mat())
        using (Mat sqsum = new Mat())
        {
            CvInvoke.Integral(matSrc, sum, sqsum, null, DepthType.Cv64F);

            int tW = pTemplData.VecPyramid[iLayer].Width;
            int tH = pTemplData.VecPyramid[iLayer].Height;
            int resW = matResult.Width, resH = matResult.Height;

            double dTemplMean0 = pTemplData.VecTemplMean[iLayer].V0;
            double dTemplNorm = pTemplData.VecTemplNorm[iLayer];
            double dInvArea = pTemplData.VecInvArea[iLayer];

            // 读取积分图数据到托管数组
            int sumW = sum.Width, sumH = sum.Height;
            double[] sumData = new double[sumW * sumH];
            double[] sqData = new double[sumW * sumH];
            Marshal.Copy(sum.DataPointer, sumData, 0, sumData.Length);
            Marshal.Copy(sqsum.DataPointer, sqData, 0, sqData.Length);

            // 读取匹配结果（CCORR值）
            float[] resultData = new float[resW * resH];
            Marshal.Copy(matResult.DataPointer, resultData, 0, resultData.Length);

            int sumStep = sumW;

            for (int i = 0; i < resH; i++)
            {
                int resultIdx = i * resW;
                int idx0 = i * sumStep;
                int idx1 = i * sumStep + tW;
                int idx2 = (i + tH) * sumStep;
                int idx3 = (i + tH) * sumStep + tW;

                for (int j = 0; j < resW; j++, resultIdx++)
                {
                    int ii0 = idx0 + j, ii1 = idx1 + j, ii2 = idx2 + j, ii3 = idx3 + j;

                    double sumVal = sumData[ii0] - sumData[ii1] - sumData[ii2] + sumData[ii3];
                    double wndMean2 = sumVal * sumVal * dInvArea;

                    double num = resultData[resultIdx] - sumVal * dTemplMean0;

                    double sqVal = sqData[ii0] - sqData[ii1] - sqData[ii2] + sqData[ii3];
                    double diff2 = Math.Max(sqVal - wndMean2, 0);

                    double t;
                    if (diff2 <= Math.Min(0.5, 10 * float.Epsilon * sqVal))
                        t = 0;
                    else
                        t = Math.Sqrt(diff2) * dTemplNorm;

                    if (Math.Abs(num) < t)
                        num /= t;
                    else if (Math.Abs(num) < t * 1.125)
                        num = num > 0 ? 1 : -1;
                    else
                        num = 0;

                    resultData[resultIdx] = (float)num;
                }
            }

            Marshal.Copy(resultData, 0, matResult.DataPointer, resultData.Length);
        }
    }

    /// <summary>
    /// 单次模板匹配：先执行 CCORR 互相关，再归一化为 NCC 分数
    /// </summary>
    /// <param name="matSrc">场景图像</param>
    /// <param name="pTemplData">模板金字塔数据</param>
    /// <param name="matResult">输出的 NCC 匹配分数图</param>
    /// <param name="iLayer">当前金字塔层</param>
    /// <param name="bUseSIMD">是否使用 SIMD 加速（预留）</param>
    private static void MatchTemplate(Mat matSrc, TemplData pTemplData, Mat matResult, int iLayer, bool bUseSIMD)
    {
        CvInvoke.MatchTemplate(matSrc, pTemplData.VecPyramid[iLayer], matResult,
            TemplateMatchingType.Ccorr);
        CCOEFFDenominator(matSrc, pTemplData, matResult, iLayer);
    }

    // ==================== 极值搜索 ====================

    /// <summary>
    /// 在当前最大位置周围涂掉一块区域后，找下一个最大值（普通版）
    /// 涂掉区域 = 以当前匹配位置为中心的 2×模板尺寸范围
    /// </summary>
    /// <param name="matResult">匹配分数图（会被修改：涂掉已找到区域）</param>
    /// <param name="ptMaxLoc">当前已找到的最大值位置</param>
    /// <param name="sizeTemplate">模板尺寸</param>
    /// <param name="dMaxValue">输出：下一个最大值</param>
    /// <param name="dMaxOverlap">允许的重叠比例，控制涂掉区域的大小</param>
    /// <returns>下一个最大值的位置</returns>
    private static Point GetNextMaxLoc(Mat matResult, Point ptMaxLoc,
        Size sizeTemplate, out double dMaxValue, double dMaxOverlap)
    {
        int iStartX = (int)(ptMaxLoc.X - sizeTemplate.Width * (1 - dMaxOverlap));
        int iStartY = (int)(ptMaxLoc.Y - sizeTemplate.Height * (1 - dMaxOverlap));
        int iW = (int)(2 * sizeTemplate.Width * (1 - dMaxOverlap));
        int iH = (int)(2 * sizeTemplate.Height * (1 - dMaxOverlap));

        iStartX = Math.Max(0, iStartX); iStartY = Math.Max(0, iStartY);
        if (iStartX + iW > matResult.Width) iW = matResult.Width - iStartX;
        if (iStartY + iH > matResult.Height) iH = matResult.Height - iStartY;

        CvInvoke.Rectangle(matResult,
            new Rectangle(iStartX, iStartY, iW, iH),
            new MCvScalar(-1), -1);

        Point ptNew = new Point();
        double dummyMin2 = 0; Point dummyLoc2 = new Point();
        dMaxValue = 0;
        CvInvoke.MinMaxLoc(matResult, ref dummyMin2, ref dMaxValue, ref dummyLoc2, ref ptNew);
        return ptNew;
    }

    /// <summary>
    /// 在当前最大位置周围涂掉一块区域后，找下一个最大值（BlockMax 加速版）
    /// 与普通版的区别：使用 BlockMax 分块管理，只更新受影响的块
    /// </summary>
    private static Point GetNextMaxLoc(Mat matResult, Point ptMaxLoc,
        Size sizeTemplate, out double dMaxValue, double dMaxOverlap, BlockMax blockMax)
    {
        int iStartX = (int)(ptMaxLoc.X - sizeTemplate.Width * (1 - dMaxOverlap));
        int iStartY = (int)(ptMaxLoc.Y - sizeTemplate.Height * (1 - dMaxOverlap));
        int iW = (int)(2 * sizeTemplate.Width * (1 - dMaxOverlap));
        int iH = (int)(2 * sizeTemplate.Height * (1 - dMaxOverlap));

        iStartX = Math.Max(0, iStartX); iStartY = Math.Max(0, iStartY);
        if (iStartX + iW > matResult.Width) iW = matResult.Width - iStartX;
        if (iStartY + iH > matResult.Height) iH = matResult.Height - iStartY;

        Rectangle rectIgnore = new Rectangle(iStartX, iStartY, iW, iH);
        CvInvoke.Rectangle(matResult, rectIgnore, new MCvScalar(-1), -1);
        blockMax.UpdateMax(rectIgnore);
        Point ptReturn;
        blockMax.GetMaxValueLoc(out dMaxValue, out ptReturn);
        return ptReturn;
    }

    // ==================== 旋转 ROI ====================

    /// <summary>
    /// 从场景图中提取旋转后的局部 ROI 区域
    /// 逐层精化时不再旋转整张图，而是只裁剪模板周围的 6 像素 padding 区域并旋转
    /// </summary>
    /// <param name="matSrc">场景图像（当前金字塔层）</param>
    /// <param name="size">模板在当前层的尺寸</param>
    /// <param name="ptLT">模板左上角在场景图中的坐标</param>
    /// <param name="dAngle">旋转角度（度）</param>
    /// <param name="matROI">输出：旋转后的 ROI 图像</param>
    private static void GetRotatedROI(Mat matSrc, Size size,
        PointF ptLT, double dAngle, out Mat matROI)
    {
        double dR = dAngle * Const.D2R;
        PointF ptC = new PointF((matSrc.Width - 1) / 2f, (matSrc.Height - 1) / 2f);
        PointF ptLTRot = PtRotatePt2f(ptLT, ptC, dR);
        Size sizePad = new Size(size.Width + 6, size.Height + 6);

        using (var M = new Mat())
        {
            CvInvoke.GetRotationMatrix2D(ptC, dAngle, 1, M);
            double[] mData = new double[6];
            Marshal.Copy(M.DataPointer, mData, 0, 6);
            mData[2] -= ptLTRot.X - 3;
            mData[5] -= ptLTRot.Y - 3;
            Marshal.Copy(mData, 0, M.DataPointer, 6);

            matROI = new Mat();
            CvInvoke.WarpAffine(matSrc, matROI, M, sizePad, Inter.Linear,
                Warp.Default, BorderType.Constant, new MCvScalar(0));
        }
    }

    // ==================== 旋转矩形构造 ====================

    /// <summary>
    /// 由三个角点构建旋转矩形
    /// p1=左上, p2=右上, p3=右下，通过向量运算确定宽高和旋转角度
    /// </summary>
    /// <param name="p1">左上角点</param>
    /// <param name="p2">右上角点</param>
    /// <param name="p3">右下角点</param>
    /// <returns>OpenCV 的旋转矩形</returns>
    private static RotatedRect RotatedRect2(PointF p1, PointF p2, PointF p3)
    {
        PointF center = new PointF(0.5f * (p1.X + p3.X), 0.5f * (p1.Y + p3.Y));
        float v1x = p1.X - p2.X, v1y = p1.Y - p2.Y;
        float v2x = p2.X - p3.X, v2y = p2.Y - p3.Y;

        int wd_i = Math.Abs(v2y) < Math.Abs(v2x) ? 1 : 0;
        int ht_i = (wd_i + 1) % 2;
        float[] vsx = { v1x, v2x }, vsy = { v1y, v2y };

        float angle = (float)(Math.Atan2(vsy[wd_i], vsx[wd_i]) * Const.R2D);
        float width = (float)Math.Sqrt(vsx[wd_i] * vsx[wd_i] + vsy[wd_i] * vsy[wd_i]);
        float height = (float)Math.Sqrt(vsx[ht_i] * vsx[ht_i] + vsy[ht_i] * vsy[ht_i]);

        return new RotatedRect(center, new SizeF(width, height), angle);
    }

    // ==================== 亚像素二次拟合 ====================

    /// <summary>
    /// 亚像素精度估计：在像素级最佳位置周围采样 3×3×3=27 个点，
    /// 用最小二乘拟合二次曲面 f(x,y,θ)，求解曲面的极值点得到亚像素坐标和角度
    /// 原理详见代码外的文档说明
    /// </summary>
    /// <param name="vec">当前层所有角度候选的匹配参数列表（含 VecResult 邻域分数）</param>
    /// <param name="dNewX">输出：亚像素精修后的 X 坐标偏移量</param>
    /// <param name="dNewY">输出：亚像素精修后的 Y 坐标偏移量</param>
    /// <param name="dNewAngle">输出：亚像素精修后的角度修正量</param>
    /// <param name="dAngleStep">当前层的角度搜索步长</param>
    /// <param name="iMaxScoreIndex">最佳分数在 vec 中的索引（必须是中间的那个角度）</param>
    /// <returns>拟合成功返回 true</returns>
    private static bool SubPixEstimation(List<MatchParameter> vec,
        out double dNewX, out double dNewY, out double dNewAngle,
        double dAngleStep, int iMaxScoreIndex)
    {
        dNewX = dNewY = dNewAngle = 0;

        using (Mat matA = new Mat(27, 10, DepthType.Cv64F, 1))
        using (Mat matS = new Mat(27, 1, DepthType.Cv64F, 1))
        {
            double dXm = vec[iMaxScoreIndex].Pt.X;
            double dYm = vec[iMaxScoreIndex].Pt.Y;
            double dTm = vec[iMaxScoreIndex].DMatchAngle;

            int iRow = 0;
            for (int theta = 0; theta <= 2; theta++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        double dX = dXm + x, dY = dYm + y;
                        double dT = (dTm + (theta - 1) * dAngleStep) * Const.D2R;
                        double[] aVals = { dX * dX, dY * dY, dT * dT, dX * dY, dX * dT, dY * dT, dX, dY, dT, 1.0 };
                        double sVal = vec[iMaxScoreIndex + (theta - 1)].VecResult[x + 1, y + 1];

                        // 使用 Marshal 设置 Mat 元素
                        int rowStart = iRow;
                        IntPtr ptrA = IntPtr.Add(matA.DataPointer, rowStart * 10 * sizeof(double));
                        Marshal.Copy(aVals, 0, ptrA, 10);
                        IntPtr ptrS = IntPtr.Add(matS.DataPointer, rowStart * sizeof(double));
                        Marshal.Copy(new[] { sVal }, 0, ptrS, 1);
                        iRow++;
                    }
                }
            }

            // z = (A^T A)^-1 A^T S
            using (Mat matAT = new Mat())
            using (Mat matATA = new Mat())
            using (Mat matZ = new Mat())
            {
                CvInvoke.Transpose(matA, matAT);
                CvInvoke.Gemm(matAT, matA, 1, null, 0, matATA);
                CvInvoke.Invert(matATA, matATA, DecompMethod.Svd);
                using (Mat matTmp = new Mat())
                {
                    CvInvoke.Gemm(matATA, matAT, 1, null, 0, matTmp);
                    CvInvoke.Gemm(matTmp, matS, 1, null, 0, matZ);
                }

                double[] dZ = new double[10];
                Marshal.Copy(matZ.DataPointer, dZ, 0, 10);

                // 解 3x3 线性方程
                double k0 = dZ[0], k1 = dZ[1], k2 = dZ[2], k3 = dZ[3], k4 = dZ[4], k5 = dZ[5];
                double k6 = dZ[6], k7 = dZ[7], k8 = dZ[8];

                // | 2k0  k3  k4 |
                // |  k3 2k1  k5 |
                // |  k4  k5 2k2 |
                double a11 = 2 * k0, a12 = k3, a13 = k4;
                double a21 = k3, a22 = 2 * k1, a23 = k5;
                double a31 = k4, a32 = k5, a33 = 2 * k2;
                double b1 = -k6, b2 = -k7, b3 = -k8;

                // 3x3 矩阵求逆 + 乘法
                double det = a11 * (a22 * a33 - a23 * a32)
                             - a12 * (a21 * a33 - a23 * a31)
                             + a13 * (a21 * a32 - a22 * a31);

                if (Math.Abs(det) < 1e-10) return false;

                double invDet = 1.0 / det;
                double dx = ((a22 * a33 - a23 * a32) * b1
                             + (a13 * a32 - a12 * a33) * b2
                             + (a12 * a23 - a13 * a22) * b3) * invDet;
                double dy = ((a23 * a31 - a21 * a33) * b1
                             + (a11 * a33 - a13 * a31) * b2
                             + (a13 * a21 - a11 * a23) * b3) * invDet;
                double dt = ((a21 * a32 - a22 * a31) * b1
                             + (a12 * a31 - a11 * a32) * b2
                             + (a11 * a22 - a12 * a21) * b3) * invDet;

                dNewX = dx;
                dNewY = dy;
                dNewAngle = dt * Const.R2D;
            }
        }
        return true;
    }

    // ==================== 后处理 ====================

    /// <summary>
    /// 按分数阈值过滤候选：先按分数降序排序，移除低于阈值的所有候选
    /// </summary>
    /// <param name="vec">候选列表（会被修改）</param>
    /// <param name="dScore">最低分数阈值</param>
    private static void FilterWithScore(List<MatchParameter> vec, double dScore)
    {
        vec.Sort((a, b) => b.DMatchScore.CompareTo(a.DMatchScore));
        int idx = vec.FindIndex(v => v.DMatchScore < dScore);
        if (idx >= 0) vec.RemoveRange(idx, vec.Count - idx);
    }

    /// <summary>
    /// 旋转矩形 NMS 去重（非极大值抑制）
    /// 两两比较旋转矩形的重叠度（IoU），重叠超过阈值的只保留分数较高的那个
    /// </summary>
    /// <param name="vec">候选列表（会被修改）</param>
    /// <param name="method">匹配方法（未使用，保留参数）</param>
    /// <param name="dMaxOverLap">IoU 阈值，超过此值视为重复</param>
    private static void FilterWithRotatedRect(List<MatchParameter> vec,
        TemplateMatchingType method, double dMaxOverLap)
    {
        int n = vec.Count;
        for (int i = 0; i < n - 1; i++)
        {
            if (vec[i].BDelete) continue;
            for (int j = i + 1; j < n; j++)
            {
                if (vec[j].BDelete) continue;

                using (var interPts = new VectorOfPointF())
                {
                    var interType = CvInvoke.RotatedRectangleIntersection(
                        vec[i].RectR, vec[j].RectR, interPts);

                    if (interType == RectIntersectType.None) continue;

                    if (interType == RectIntersectType.Full)
                    {
                        int del = (vec[i].DMatchScore >= vec[j].DMatchScore) ? j : i;
                        vec[del].BDelete = true;
                    }
                    else if (interPts.Size >= 3)
                    {
                        using (var hull = new VectorOfPointF())
                        {
                            CvInvoke.ConvexHull(interPts, hull, false, true);
                            double dArea = CvInvoke.ContourArea(hull, false);
                            double dRatio = dArea / (vec[i].RectR.Size.Width * vec[i].RectR.Size.Height);
                            if (dRatio > dMaxOverLap)
                            {
                                int del = (vec[i].DMatchScore >= vec[j].DMatchScore) ? j : i;
                                vec[del].BDelete = true;
                            }
                        }
                    }
                }
            }
        }
        vec.RemoveAll(v => v.BDelete);
    }

    // ==================== 主匹配流程 ====================

    /// <summary>
    /// 执行模板匹配，返回匹配结果列表
    /// 五步流程：
    ///   1. 构建场景图金字塔
    ///   2. 顶层多角度粗匹配（Parallel.For 并行）
    ///   3. 逐层精细匹配 + 亚像素估计
    ///   4. 旋转矩形 NMS 去重
    ///   5. 组装 MatchResult 输出
    /// </summary>
    /// <param name="image">场景图像（灰度单通道）</param>
    /// <param name="matchResults">输出的匹配结果列表</param>
    /// <returns>匹配结果数量，负数表示错误码</returns>
    public int Match(Mat image, out List<MatchResult> matchResults)
    {
        matchResults = new List<MatchResult>();

        if (image == null || image.IsEmpty) return -1;
        if (_templateImage == null || _templateImage.IsEmpty || _templateImage.NumberOfChannels != 1)
            return -5;

        int tW = _templateImage.Width, tH = _templateImage.Height;
        int iW = image.Width, iH = image.Height;
        if ((tW < iW && tH > iH) || (tW > iW && tH < iH)) return -2;
        if (tW * (long)tH > iW * (long)iH) return -3;
        if (!_templData.BIsPatternLearned) return -4;

        // 金字塔层数
        int iTopLayer = GetTopLayer(_templateImage, (int)Math.Sqrt(_param.MinArea));

        // 待搜图金字塔
        var vecMatSrcPyr = new List<Mat>();
        using (var pyramidSrc = new VectorOfMat())
        {
            CvInvoke.BuildPyramid(image, pyramidSrc, iTopLayer);
            for (int i = 0; i < pyramidSrc.Size; i++)
                vecMatSrcPyr.Add(pyramidSrc[i].Clone());
        }

        TemplData pTemplData = _templData;

        // 顶层角度步长
        double dAngleStep = Math.Atan(2.0 / Math.Max(
            pTemplData.VecPyramid[iTopLayer].Width,
            pTemplData.VecPyramid[iTopLayer].Height)) * Const.R2D;

        var vecAngles = new List<double>();
        if (_param.Angle < Const.VisionTolerance)
            vecAngles.Add(0.0);
        else
        {
            for (double a = 0; a < _param.Angle + dAngleStep; a += dAngleStep)
                vecAngles.Add(a);
            for (double a = -dAngleStep; a > -_param.Angle - dAngleStep; a -= dAngleStep)
                vecAngles.Add(a);
        }

        int iTopSrcW = vecMatSrcPyr[iTopLayer].Width;
        int iTopSrcH = vecMatSrcPyr[iTopLayer].Height;
        PointF ptCenter = new PointF((iTopSrcW - 1) / 2f, (iTopSrcH - 1) / 2f);

        // 各层最低分数
        var vecLayerScore = new double[iTopLayer + 1];
        for (int i = 0; i <= iTopLayer; i++) vecLayerScore[i] = _param.ScoreThreshold;
        for (int iL = 1; iL <= iTopLayer; iL++) vecLayerScore[iL] = vecLayerScore[iL - 1] * 0.9;

        Size sizePat = pTemplData.VecPyramid[iTopLayer].Size;
        double areaRatio = (double)(vecMatSrcPyr[iTopLayer].Width * vecMatSrcPyr[iTopLayer].Height)
                           / (sizePat.Width * sizePat.Height);
        bool bCalMaxByBlock = areaRatio > 500 && _param.MaxCount > 10;

        // ===== Step 2: 顶层多角度粗匹配 =====
        var vecMatchParameter = new List<MatchParameter>();
        var lockObj = new object();

        Parallel.For(0, vecAngles.Count, i =>
        {
            double dAngle = vecAngles[i];

            var M = new Mat();
            CvInvoke.GetRotationMatrix2D(ptCenter, dAngle, 1, M);

            Size sizeBest = GetBestRotationSize(
                vecMatSrcPyr[iTopLayer].Size,
                pTemplData.VecPyramid[iTopLayer].Size, dAngle);

            float fTX = (sizeBest.Width - 1) / 2f - ptCenter.X;
            float fTY = (sizeBest.Height - 1) / 2f - ptCenter.Y;
            // 读取旋转矩阵并修改平移分量
            double[] mData = new double[6];
            Marshal.Copy(M.DataPointer, mData, 0, 6);
            mData[2] += fTX;
            mData[5] += fTY;
            Marshal.Copy(mData, 0, M.DataPointer, 6);

            using (Mat matRotatedSrc = new Mat())
            using (Mat matResult = new Mat())
            {
                CvInvoke.WarpAffine(vecMatSrcPyr[iTopLayer], matRotatedSrc, M, sizeBest,
                    Inter.Linear, Warp.Default, BorderType.Constant,
                    new MCvScalar(pTemplData.IBorderColor));

                MatchTemplate(matRotatedSrc, pTemplData, matResult, iTopLayer, false);

                if (bCalMaxByBlock)
                {
                    BlockMax blockMax = new BlockMax(matResult, pTemplData.VecPyramid[iTopLayer].Size);
                    double dMaxVal; Point ptMaxLoc;
                    blockMax.GetMaxValueLoc(out dMaxVal, out ptMaxLoc);

                    if (dMaxVal < vecLayerScore[iTopLayer]) { M.Dispose(); return; }

                    lock (lockObj)
                        vecMatchParameter.Add(new MatchParameter(
                            new PointF(ptMaxLoc.X - fTX, ptMaxLoc.Y - fTY), dMaxVal, dAngle));

                    for (int j = 0; j < _param.MaxCount + Const.MatchCandidateNum - 1; j++)
                    {
                        double dValue;
                        ptMaxLoc = GetNextMaxLoc(matResult, ptMaxLoc,
                            pTemplData.VecPyramid[iTopLayer].Size,
                            out dValue, _param.IouThreshold, blockMax);
                        if (dValue < vecLayerScore[iTopLayer]) break;
                        lock (lockObj)
                            vecMatchParameter.Add(new MatchParameter(
                                new PointF(ptMaxLoc.X - fTX, ptMaxLoc.Y - fTY), dValue, dAngle));
                    }
                }
                else
                {
                    Point ptMaxLoc = new Point();
                    double dMaxVal = 0, minV3 = 0; Point minLoc3 = new Point();
                    CvInvoke.MinMaxLoc(matResult, ref minV3, ref dMaxVal, ref minLoc3, ref ptMaxLoc);

                    if (dMaxVal < vecLayerScore[iTopLayer]) { M.Dispose(); return; }

                    lock (lockObj)
                        vecMatchParameter.Add(new MatchParameter(
                            new PointF(ptMaxLoc.X - fTX, ptMaxLoc.Y - fTY), dMaxVal, dAngle));

                    for (int j = 0; j < _param.MaxCount + Const.MatchCandidateNum - 1; j++)
                    {
                        double dValue;
                        ptMaxLoc = GetNextMaxLoc(matResult, ptMaxLoc,
                            pTemplData.VecPyramid[iTopLayer].Size,
                            out dValue, _param.IouThreshold);
                        if (dValue < vecLayerScore[iTopLayer]) break;
                        lock (lockObj)
                            vecMatchParameter.Add(new MatchParameter(
                                new PointF(ptMaxLoc.X - fTX, ptMaxLoc.Y - fTY), dValue, dAngle));
                    }
                }
            }
            M.Dispose();
        });

        vecMatchParameter.Sort((a, b) => b.DMatchScore.CompareTo(a.DMatchScore));

        // ===== Step 3: 逐层精细匹配 =====
        int iDstW = pTemplData.VecPyramid[iTopLayer].Width;
        int iDstH = pTemplData.VecPyramid[iTopLayer].Height;
        bool bSubPixelEstimation = SubPixel;
        int iStopLayer = StopLayer1 ? 1 : 0;

        var vecAllResult = new List<MatchParameter>();

        for (int idx = 0; idx < vecMatchParameter.Count; idx++)
        {
            var mp = vecMatchParameter[idx];
            double dRAngle = -mp.DMatchAngle * Const.D2R;
            PointF ptLT = PtRotatePt2f(mp.Pt, ptCenter, dRAngle);

            if (iTopLayer <= iStopLayer)
            {
                mp.Pt = new PointF(ptLT.X * (iTopLayer == 0 ? 1 : 2),
                    ptLT.Y * (iTopLayer == 0 ? 1 : 2));
                vecAllResult.Add(mp);
            }
            else
            {
                for (int iLayer = iTopLayer - 1; iLayer >= iStopLayer; iLayer--)
                {
                    double dLayerAngleStep = Math.Atan(2.0 / Math.Max(
                        pTemplData.VecPyramid[iLayer].Width,
                        pTemplData.VecPyramid[iLayer].Height)) * Const.R2D;

                    var vecLayerAngles = new List<double>();
                    double dMatchedAngle = mp.DMatchAngle;
                    if (_param.Angle < Const.VisionTolerance)
                        vecLayerAngles.Add(0.0);
                    else
                        for (int a = -2; a <= 2; a++)
                            vecLayerAngles.Add(dMatchedAngle + dLayerAngleStep * a);

                    PointF ptSrcCenter = new PointF(
                        (vecMatSrcPyr[iLayer].Width - 1) / 2f,
                        (vecMatSrcPyr[iLayer].Height - 1) / 2f);

                    int iSizeA = vecLayerAngles.Count;
                    var vecNewMatchParameter = new List<MatchParameter>(iSizeA);
                    int iMaxScoreIndex = 0;
                    double dBigValue = -1;

                    for (int j = 0; j < iSizeA; j++)
                    {
                        double dAng = vecLayerAngles[j];
                        Mat matRotatedSrc, matRes = new Mat();

                        GetRotatedROI(vecMatSrcPyr[iLayer],
                            pTemplData.VecPyramid[iLayer].Size,
                            new PointF(ptLT.X * 2, ptLT.Y * 2),
                            dAng, out matRotatedSrc);

                        MatchTemplate(matRotatedSrc, pTemplData, matRes, iLayer, true);

                        Point ptMaxLoc = new Point();
                        double dMaxValue = 0, minV4 = 0; Point minLoc4 = new Point();
                        CvInvoke.MinMaxLoc(matRes, ref minV4, ref dMaxValue, ref minLoc4, ref ptMaxLoc);

                        var newMp = new MatchParameter(ptMaxLoc, dMaxValue, dAng);
                        vecNewMatchParameter.Add(newMp);

                        if (newMp.DMatchScore > dBigValue)
                        {
                            iMaxScoreIndex = j;
                            dBigValue = newMp.DMatchScore;
                        }

                        // 边界检测 + 采样邻域分数
                        if (ptMaxLoc.X == 0 || ptMaxLoc.Y == 0 ||
                            ptMaxLoc.X == matRes.Width - 1 || ptMaxLoc.Y == matRes.Height - 1)
                            newMp.BPosOnBorder = true;

                        if (!newMp.BPosOnBorder)
                        {
                            float[] resData = new float[matRes.Width * matRes.Height];
                            Marshal.Copy(matRes.DataPointer, resData, 0, resData.Length);
                            int matW = matRes.Width;
                            for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                newMp.VecResult[dx + 1, dy + 1] =
                                    resData[(ptMaxLoc.Y + dy) * matW + (ptMaxLoc.X + dx)];
                        }

                        matRotatedSrc.Dispose();
                        matRes.Dispose();
                    }

                    if (vecNewMatchParameter[iMaxScoreIndex].DMatchScore < vecLayerScore[iLayer])
                        break;

                    // 亚像素估计
                    if (bSubPixelEstimation && iLayer == 0
                                            && !vecNewMatchParameter[iMaxScoreIndex].BPosOnBorder
                                            && iMaxScoreIndex > 0 && iMaxScoreIndex < iSizeA - 1)
                    {
                        double dNewX, dNewY, dNewAngle;
                        SubPixEstimation(vecNewMatchParameter,
                            out dNewX, out dNewY, out dNewAngle,
                            dLayerAngleStep, iMaxScoreIndex);
                        vecNewMatchParameter[iMaxScoreIndex].Pt = new PointF((float)dNewX, (float)dNewY);
                        vecNewMatchParameter[iMaxScoreIndex].DMatchAngle = dNewAngle;
                    }

                    double dNewMatchAngle = vecNewMatchParameter[iMaxScoreIndex].DMatchAngle;

                    // 坐标变换
                    PointF ptLT2 = new PointF(ptLT.X * 2, ptLT.Y * 2);
                    PointF ptLTRot = PtRotatePt2f(ptLT2, ptSrcCenter, dNewMatchAngle * Const.D2R);
                    PointF ptPaddingLT = new PointF(ptLTRot.X - 3, ptLTRot.Y - 3);

                    PointF pt = new PointF(
                        vecNewMatchParameter[iMaxScoreIndex].Pt.X + ptPaddingLT.X,
                        vecNewMatchParameter[iMaxScoreIndex].Pt.Y + ptPaddingLT.Y);
                    pt = PtRotatePt2f(pt, ptSrcCenter, -dNewMatchAngle * Const.D2R);

                    if (iLayer == iStopLayer)
                    {
                        vecNewMatchParameter[iMaxScoreIndex].Pt = new PointF(
                            pt.X * (iStopLayer == 0 ? 1 : 2),
                            pt.Y * (iStopLayer == 0 ? 1 : 2));
                        vecAllResult.Add(vecNewMatchParameter[iMaxScoreIndex]);
                    }
                    else
                    {
                        mp.DMatchAngle = dNewMatchAngle;
                        ptLT = pt;
                    }
                }
            }
        }

        FilterWithScore(vecAllResult, _param.ScoreThreshold);

        // ===== Step 4: 旋转矩形 NMS =====
        iDstW = pTemplData.VecPyramid[iStopLayer].Width * (iStopLayer == 0 ? 1 : 2);
        iDstH = pTemplData.VecPyramid[iStopLayer].Height * (iStopLayer == 0 ? 1 : 2);

        foreach (var mp in vecAllResult)
        {
            double dRA = -mp.DMatchAngle * Const.D2R;
            PointF ptLT2 = mp.Pt;
            PointF ptRT = new PointF((float)(ptLT2.X + iDstW * Math.Cos(dRA)),
                (float)(ptLT2.Y - iDstW * Math.Sin(dRA)));
            PointF ptRB = new PointF((float)(ptRT.X + iDstH * Math.Sin(dRA)),
                (float)(ptRT.Y + iDstH * Math.Cos(dRA)));
            mp.RectR = RotatedRect2(ptLT2, ptRT, ptRB);
        }

        FilterWithRotatedRect(vecAllResult, TemplateMatchingType.CcoeffNormed, _param.IouThreshold);
        vecAllResult.Sort((a, b) => b.DMatchScore.CompareTo(a.DMatchScore));

        // ===== Step 5: 组装结果 =====
        int iW0 = pTemplData.VecPyramid[0].Width;
        int iH0 = pTemplData.VecPyramid[0].Height;
        int resultCount = Math.Min(vecAllResult.Count, _param.MaxCount);

        for (int i = 0; i < resultCount; i++)
        {
            var mp = vecAllResult[i];
            double dRA = -mp.DMatchAngle * Const.D2R;

            var result = new MatchResult();
            result.LeftTop = mp.Pt;
            result.RightTop = new PointF(
                (float)(result.LeftTop.X + iW0 * Math.Cos(dRA)),
                (float)(result.LeftTop.Y - iW0 * Math.Sin(dRA)));
            result.LeftBottom = new PointF(
                (float)(result.LeftTop.X + iH0 * Math.Sin(dRA)),
                (float)(result.LeftTop.Y + iH0 * Math.Cos(dRA)));
            result.RightBottom = new PointF(
                (float)(result.RightTop.X + iH0 * Math.Sin(dRA)),
                (float)(result.RightTop.Y + iH0 * Math.Cos(dRA)));
            result.Center = new PointF(
                (result.LeftTop.X + result.RightTop.X + result.RightBottom.X + result.LeftBottom.X) / 4,
                (result.LeftTop.Y + result.RightTop.Y + result.RightBottom.Y + result.LeftBottom.Y) / 4);

            double angle = -mp.DMatchAngle;
            if (angle < -180) angle += 360;
            if (angle > 180) angle -= 360;
            result.Angle = angle;
            result.Score = mp.DMatchScore;

            matchResults.Add(result);
        }

        foreach (var m in vecMatSrcPyr) m.Dispose();
        return matchResults.Count;
    }

    // ==================== 可视化 ====================

    /// <summary>
    /// 在图像上绘制匹配结果（绿色矩形框 + 分数标签）
    /// 保存为 demo.png 并显示窗口
    /// </summary>
    /// <param name="frame">要绘制的图像（会被克隆，不会修改原图）</param>
    /// <param name="matchResults">匹配结果列表</param>
    public void DrawResult(Mat frame, List<MatchResult> matchResults)
    {
        if (frame == null || frame.IsEmpty || matchResults == null || matchResults.Count == 0) return;

        Mat drawFrame = frame.Clone();
        if (drawFrame.NumberOfChannels == 1)
            CvInvoke.CvtColor(drawFrame, drawFrame, ColorConversion.Gray2Bgr);

        foreach (var r in matchResults)
        {
            var pts = new Point[]
            {
                new Point((int)Math.Round(r.LeftTop.X), (int)Math.Round(r.LeftTop.Y)),
                new Point((int)Math.Round(r.RightTop.X), (int)Math.Round(r.RightTop.Y)),
                new Point((int)Math.Round(r.RightBottom.X), (int)Math.Round(r.RightBottom.Y)),
                new Point((int)Math.Round(r.LeftBottom.X), (int)Math.Round(r.LeftBottom.Y)),
            };

            using (var ptsVec = new VectorOfPoint(pts))
                CvInvoke.Polylines(drawFrame, ptsVec, true, new MCvScalar(0, 255, 0), 2);

            CvInvoke.PutText(drawFrame, r.Score.ToString("F3"),
                new Point((int)r.LeftTop.X, (int)r.LeftTop.Y - 5),
                FontFace.HersheyPlain, 1.0, new MCvScalar(0, 255, 0), 1);
        }

        drawFrame.Save("demo.png");
        CvInvoke.Imshow("Result", drawFrame);
        CvInvoke.WaitKey();
        drawFrame.Dispose();
    }
}