using System.Drawing;
using Emgu.CV;
using Emgu.CV.LineDescriptor;

namespace VisionToolkit.EdgeFinder;

/// <summary>
/// LSD (Line Segment Detector) 直线检测工具。
/// 基于梯度方向聚类的全图线段检测，输出亚像素精度端点。
/// </summary>
public static class LsdEdgeFinder
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
        /// <summary>检测到的总线段数</summary>
        public int TotalDetected;
        /// <summary>其他线段的中心点（用于可视化参考）</summary>
        public PointF[]? OtherSegments;
    }

    /// <summary>
    /// 在灰度图像上运行 LSD 检测，返回最长的线段。
    /// </summary>
    /// <param name="gray">单通道 8-bit 灰度图像</param>
    /// <param name="scale">图像缩放比（2=半尺寸），默认 2</param>
    /// <param name="numOctaves">金字塔层数，默认 2</param>
    public static Result Detect(Mat gray, int scale = 2, int numOctaves = 2)
    {
        var keyLines = new VectorOfKeyLine();
        new LSDDetector().Detect(gray, keyLines, scale, numOctaves);

        var result = new Result { TotalDetected = (int)keyLines.Size };

        if (keyLines.Size == 0)
            return result;

        // 取最长线段作为最佳结果
        double bestLen = 0;
        int bestIdx = -1;
        for (int i = 0; i < keyLines.Size; i++)
        {
            var kl = keyLines[i];
            double dx = kl.EndPointX - kl.StartPointX;
            double dy = kl.EndPointY - kl.StartPointY;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > bestLen) { bestLen = len; bestIdx = i; }
        }

        var best = keyLines[bestIdx];
        float dxB = best.EndPointX - best.StartPointX;
        float dyB = best.EndPointY - best.StartPointY;

        result.Start = new PointF(best.StartPointX, best.StartPointY);
        result.End = new PointF(best.EndPointX, best.EndPointY);
        result.Length = Math.Sqrt(dxB * dxB + dyB * dyB);
        result.AngleDeg = LineAngle(best.StartPointX, best.StartPointY, best.EndPointX, best.EndPointY);

        // 收集其他线段中心点（用于可视化参考）
        if (keyLines.Size > 1)
        {
            var others = new PointF[(int)keyLines.Size - 1];
            int idx = 0;
            for (int i = 0; i < keyLines.Size; i++)
            {
                if (i == bestIdx) continue;
                others[idx++] = new PointF(
                    (keyLines[i].StartPointX + keyLines[i].EndPointX) * 0.5f,
                    (keyLines[i].StartPointY + keyLines[i].EndPointY) * 0.5f);
            }
            result.OtherSegments = others;
        }

        return result;
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
