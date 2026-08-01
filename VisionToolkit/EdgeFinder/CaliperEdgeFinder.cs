using System.Drawing;
using Emgu.CV;

namespace VisionToolkit.EdgeFinder;

/// <summary>
/// 卡尺（Caliper）亚像素边缘定位工具。
/// 多条等距投影线，每条在卡尺宽度内做像素平均 → 宽核梯度峰值定位 → RANSAC → 最小二乘直线拟合。
/// 适用于已知边缘大致方向的精确定位，精度 ~0.05 px。
/// </summary>
public static class CaliperEdgeFinder
{
    /// <summary>检测结果</summary>
    public sealed class Result
    {
        /// <summary>线段起点（亚像素）</summary>
        public PointF Start;
        /// <summary>线段终点（亚像素）</summary>
        public PointF End;
        /// <summary>线段长度（px）</summary>
        public double Length;
        /// <summary>线段角度（度，[-90,90)，0=水平）</summary>
        public double AngleDeg;
        /// <summary>所有扫描线的亚像素边点</summary>
        public PointF[] ScanPoints = [];
        /// <summary>RANSAC 内点数</summary>
        public int InlierCount;
        /// <summary>总采样点数</summary>
        public int TotalSamples;
    }

    /// <summary>
    /// 卡尺找边。
    /// </summary>
    /// <param name="gray">单通道 8-bit 灰度图像</param>
    /// <param name="edgeAngleDeg">边缘方向角（度）：0°=横边, 90°=竖边, 45°=斜边↗</param>
    /// <param name="caliperCount">卡尺数量（等距分布在边方向上），默认 20</param>
    /// <param name="caliperWidth">卡尺宽度（px），每条投影线在边方向上取 caliperWidth 宽度的像素平均，用于降噪</param>
    /// <param name="searchHalf">搜索半长（px），从 ROI 中心沿垂直方向左右各搜多远</param>
    /// <param name="inlierThreshold">RANSAC 内点距离阈（px），默认 0.8</param>
    public static unsafe Result Detect(Mat gray,
        double edgeAngleDeg = 90,
        int    caliperCount = 20,
        double caliperWidth = 5,
        double searchHalf = 40,
        double inlierThreshold = 0.8)
    {
        int rows = gray.Rows, cols = gray.Cols;
        byte* pGray = (byte*)gray.DataPointer;
        int step = (int)gray.Step;

        // ── 方向向量 ──
        double rad = edgeAngleDeg * Math.PI / 180.0;
        double edx = Math.Cos(rad);      // 沿边方向
        double edy = Math.Sin(rad);
        double pdx = -edy;               // 垂直方向（扫描方向）
        double pdy = edx;

        double imageCenterX = cols * 0.5 - 0.5;
        double imageCenterY = rows * 0.5 - 0.5;

        // ── 图像在扫描方向上的实际范围（排除边界假边） ──
        double c0 = (0 - imageCenterX) * pdx + (0 - imageCenterY) * pdy;
        double c1 = (cols - 1 - imageCenterX) * pdx + (0 - imageCenterY) * pdy;
        double c2 = (0 - imageCenterX) * pdx + (rows - 1 - imageCenterY) * pdy;
        double c3 = (cols - 1 - imageCenterX) * pdx + (rows - 1 - imageCenterY) * pdy;
        double imgMinOffset = Math.Min(Math.Min(c0, c1), Math.Min(c2, c3));
        double imgMaxOffset = Math.Max(Math.Max(c0, c1), Math.Max(c2, c3));

        const double scanRes = 0.5;            // 剖面采样步长（内部固定）
        int profileLen = (int)(2 * searchHalf / scanRes) + 1;

        // 图像边界在 profile 中的索引
        int imgStartInProfile = (int)((imgMinOffset + searchHalf) / scanRes);
        int imgEndInProfile   = (int)((imgMaxOffset + searchHalf) / scanRes);
        int validStart = Math.Max(0, imgStartInProfile);
        int validEnd   = Math.Min(profileLen - 1, imgEndInProfile);

        // ── 卡尺宽度内的采样次数 ──
        double wHalf = caliperWidth * 0.5;
        int wSamples = Math.Max(1, (int)(caliperWidth / 0.5) + 1);  // 0.5px 步长

        var edgePoints = new List<PointF>();

        // ── 沿边方向等距放置 caliperCount 条投影线 ──
        for (int i = 0; i < caliperCount; i++)
        {
            double t = (i - (caliperCount - 1) * 0.5) * searchHalf / caliperCount * 2;
            // 简化：让卡尺均匀覆盖图像的边方向跨度
            double edgeSpan = Math.Max(rows, cols);
            double spacing = edgeSpan / (caliperCount - 1);
            t = (i - (caliperCount - 1) * 0.5) * spacing;

            double baseX = imageCenterX + t * edx;
            double baseY = imageCenterY + t * edy;

            var profile = new double[profileLen];

            for (int j = 0; j < profileLen; j++)
            {
                double offset = -searchHalf + j * scanRes;
                double sx = baseX + offset * pdx;
                double sy = baseY + offset * pdy;

                // ── 卡尺宽度方向上的像素平均 ──
                double sumVal = 0;
                int validCount = 0;

                for (int wk = 0; wk < wSamples; wk++)
                {
                    double wOffset = -wHalf + wk * 0.5;
                    double wsx = sx + wOffset * edx;
                    double wsy = sy + wOffset * edy;

                    int x0 = (int)Math.Floor(wsx), y0 = (int)Math.Floor(wsy);
                    int ix1 = x0 + 1, iy1 = y0 + 1;
                    if (x0 < 0 || ix1 >= cols || y0 < 0 || iy1 >= rows)
                        continue;

                    double fx = wsx - x0, fy = wsy - y0;
                    double v00 = pGray[y0 * step + x0], v10 = pGray[y0 * step + ix1];
                    double v01 = pGray[iy1 * step + x0], v11 = pGray[iy1 * step + ix1];
                    sumVal += (1 - fx) * (1 - fy) * v00 + fx * (1 - fy) * v10
                            + (1 - fx) * fy * v01       + fx * fy * v11;
                    validCount++;
                }

                profile[j] = validCount > 0 ? sumVal / validCount : 0;
            }

            // ── 梯度峰值定位 ──
            int peakJ = FindGradientPeak(profile, validStart, validEnd, out double subOffset);
            if (peakJ < 0) continue;

            double edgeOffset = -searchHalf + (peakJ + subOffset) * scanRes;
            double ex = baseX + edgeOffset * pdx;
            double ey = baseY + edgeOffset * pdy;

            if (ex >= 0 && ex < cols && ey >= 0 && ey < rows)
                edgePoints.Add(new PointF((float)ex, (float)ey));
        }

        // ── 多个聚类时选最一致的那条边 ──
        var clusteredPoints = SelectBestCluster(edgePoints, imageCenterX, imageCenterY, pdx, pdy);

        // ── RANSAC + 最小二乘拟合 ──
        (double a, double b, double c, int inliers) = FitLineRansac(clusteredPoints, inlierThreshold);

        // ── 端点：中心点投影到直线上，沿边展开 ──
        (double px, double py) = ProjectPointToLine(imageCenterX, imageCenterY, a, b, c);
        double halfSpan = Math.Max(rows, cols) * 0.6;
        double x1 = px - halfSpan * edx, y1 = py - halfSpan * edy;
        double x2 = px + halfSpan * edx, y2 = py + halfSpan * edy;

        float fdx = (float)(x2 - x1), fdy = (float)(y2 - y1);
        float len = MathF.Sqrt(fdx * fdx + fdy * fdy);

        return new Result
        {
            Start = new PointF((float)x1, (float)y1),
            End = new PointF((float)x2, (float)y2),
            Length = len,
            AngleDeg = LineAngle((float)x1, (float)y1, (float)x2, (float)y2),
            ScanPoints = clusteredPoints.ToArray(),
            InlierCount = inliers, TotalSamples = clusteredPoints.Count
        };
    }

    // ── 宽核梯度 + 亚像素峰值定位 ──
    static int FindGradientPeak(double[] profile,
        int validStart, int validEnd, out double subOffset)
    {
        subOffset = 0;
        if (validEnd - validStart < 10) return -1;

        const int r = 3;  // 梯度核半宽：前后各取3点做平均

        // 宽核中央差分 = (后3点均值 - 前3点均值)
        var grad = new double[profile.Length];
        for (int i = validStart + r; i < validEnd - r; i++)
        {
            double front = 0, back = 0;
            for (int k = 1; k <= r; k++)
            {
                front += profile[i - k];
                back  += profile[i + k];
            }
            grad[i] = (back - front) / r;
        }

        // 取梯度最强的峰（而不是离中心最近的）
        int peakIdx = -1;
        double maxGrad = 0;
        for (int i = validStart + r; i < validEnd - r; i++)
        {
            double absG = Math.Abs(grad[i]);
            if (absG > maxGrad) { maxGrad = absG; peakIdx = i; }
        }

        if (peakIdx < r || peakIdx >= profile.Length - r) return -1;

        // 3点抛物线插值
        double g0 = Math.Abs(grad[peakIdx]);
        double gm1 = Math.Abs(grad[peakIdx - 1]);
        double gp1 = Math.Abs(grad[peakIdx + 1]);
        double denom = 2 * (gm1 - 2 * g0 + gp1);
        subOffset = Math.Abs(denom) > 1e-10 ? Math.Clamp((gm1 - gp1) / denom, -1.0, 1.0) : 0;
        return peakIdx;
    }

    // ── 多个聚类时选点数最多的那个（物理边缘应该最一致） ──
    static List<PointF> SelectBestCluster(List<PointF> points,
        double centerX, double centerY, double pdx, double pdy)
    {
        if (points.Count < 3) return points;

        var offsets = new (double offset, int index)[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            double dx = points[i].X - centerX;
            double dy = points[i].Y - centerY;
            offsets[i] = (dx * pdx + dy * pdy, i);
        }

        Array.Sort(offsets, (a, b) => a.offset.CompareTo(b.offset));

        int splitAt = 0;
        double maxGap = 0;
        for (int i = 1; i < offsets.Length; i++)
        {
            double gap = offsets[i].offset - offsets[i - 1].offset;
            if (gap > maxGap) { maxGap = gap; splitAt = i; }
        }

        const double gapThreshold = 3.0;
        if (maxGap < gapThreshold || points.Count < 5)
            return points;

        // 取点数最多的聚类（物理边缘应最一致）
        int count1 = splitAt;
        int count2 = offsets.Length - splitAt;

        var result = new List<PointF>();
        if (count1 >= count2)
            for (int i = 0; i < splitAt; i++) result.Add(points[offsets[i].index]);
        else
            for (int i = splitAt; i < offsets.Length; i++) result.Add(points[offsets[i].index]);

        return result;
    }

    // ── RANSAC 直线拟合 ──
    static (double a, double b, double c, int inliers) FitLineRansac(
        List<PointF> points, double inlierThreshold)
    {
        int n = points.Count;
        if (n < 2) return (0, 0, 0, 0);
        if (n == 2)
        {
            FitLine(points, [true, true], out double a, out double b, out double c);
            return (a, b, c, 2);
        }

        const int iterations = 100;
        int bestInliers = 0;
        double bestA = 0, bestB = 0, bestC = 0;
        var rng = new Random(42);

        for (int iter = 0; iter < iterations; iter++)
        {
            int i1 = rng.Next(n), i2;
            do { i2 = rng.Next(n); } while (i2 == i1);

            double dx = points[i2].X - points[i1].X;
            double dy = points[i2].Y - points[i1].Y;
            double a = -dy, b = dx;
            double norm = Math.Sqrt(a * a + b * b);
            a /= norm; b /= norm;
            double c = -(a * points[i1].X + b * points[i1].Y);

            int count = 0;
            for (int i = 0; i < n; i++)
            {
                double dist = Math.Abs(a * points[i].X + b * points[i].Y + c);
                if (dist < inlierThreshold) count++;
            }

            if (count > bestInliers)
            {
                bestInliers = count; bestA = a; bestB = b; bestC = c;
            }
        }

        var inlierMask = new bool[n];
        for (int i = 0; i < n; i++)
        {
            double dist = Math.Abs(bestA * points[i].X + bestB * points[i].Y + bestC);
            inlierMask[i] = dist < inlierThreshold;
        }

        FitLine(points, inlierMask, out double finalA, out double finalB, out double finalC);
        return (finalA, finalB, finalC, bestInliers);
    }

    // ── 最小二乘直线拟合（ax + by + c = 0, a² + b² = 1） ──
    static void FitLine(List<PointF> points, bool[] mask,
        out double a, out double b, out double c)
    {
        int n = 0;
        double sumX = 0, sumY = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (!mask[i]) continue;
            sumX += points[i].X; sumY += points[i].Y; n++;
        }

        if (n < 2) { a = 1; b = 0; c = 0; return; }

        double mx = sumX / n, my = sumY / n;
        double sxx = 0, syy = 0, sxy = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (!mask[i]) continue;
            double dx = points[i].X - mx, dy = points[i].Y - my;
            sxx += dx * dx; syy += dy * dy; sxy += dx * dy;
        }

        double trace = sxx + syy;
        double det = sxx * syy - sxy * sxy;
        double disc = Math.Sqrt(Math.Max(0, trace * trace - 4 * det));
        double lam = (trace - disc) / 2.0;

        a = sxy;
        b = lam - sxx;
        double norm = Math.Sqrt(a * a + b * b);
        if (norm < 1e-10) { a = 1; b = 0; c = -sumX / n; return; }
        a /= norm; b /= norm;
        c = -(a * mx + b * my);
    }

    // ── 点到直线投影 ──
    static (double x, double y) ProjectPointToLine(
        double px, double py, double a, double b, double c)
    {
        double dist = a * px + b * py + c;
        return (px - a * dist, py - b * dist);
    }

    /// <summary>计算无向线段与 x 轴的夹角 [-90, 90)</summary>
    static double LineAngle(float x1, float y1, float x2, float y2)
    {
        double angle = Math.Atan2(y2 - y1, x2 - x1) * 180.0 / Math.PI;
        while (angle < -90) angle += 180.0;
        while (angle >= 90) angle -= 180.0;
        return angle;
    }
}
