using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using AFOCS.VisionEditor.Models;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Drawing;
using Emgu.CV.Util;
using VisionToolkit.EdgeFinder;
using VisionToolkit.TemplateMatcher;

namespace AFOCS.VisionEditor.Services;

/// <summary>
/// 视觉流水线执行服务 —— 按 NCC → 找边1 → 找边2 → 找点 顺序执行，
/// 每步成功后在一个 clone 的彩色 Mat 上绘制结果，最后返回 BitmapSource。
/// </summary>
public class VisionExecutionService
{
    /// <summary>
    /// 执行完整流水线，结果写回 template，返回带绘图的 BitmapSource
    /// </summary>
    public BitmapSource? Execute(string imagePath, VisionTemplate template, Action<string, bool>? progress = null)
    {
        using var grayImage = CvInvoke.Imread(imagePath, ImreadModes.Grayscale);
        if (grayImage == null || grayImage.IsEmpty)
        {
            progress?.Invoke("无法加载图片", false);
            return null;
        }

        // Clone 一份彩色图用于画结果
        using var drawMat = CvInvoke.Imread(imagePath, ImreadModes.ColorRgb);
        if (drawMat == null || drawMat.IsEmpty)
        {
            progress?.Invoke("无法加载彩色图片", false);
            return null;
        }

        // Step 1: NCC
        if (template.Ncc.IsEnabled)
        {
            ExecuteNcc(grayImage, template.Ncc, progress);
            if (template.Ncc.ResultScore > 0)
                DrawNccResult(drawMat, template.Ncc);
        }

        // Step 2: 找边 1
        if (template.EdgeFind1.IsEnabled)
        {
            ExecuteEdgeFind(grayImage, template.EdgeFind1, "找边1", progress);
            DrawEdgeResult(drawMat, template.EdgeFind1);
        }

        // Step 3: 找边 2
        if (template.EdgeFind2.IsEnabled)
        {
            ExecuteEdgeFind(grayImage, template.EdgeFind2, "找边2", progress);
            DrawEdgeResult(drawMat, template.EdgeFind2);
        }

        // Step 4: 找点
        if (template.PointFind.IsEnabled)
        {
            ExecutePointFind(template.EdgeFind1, template.EdgeFind2, template.PointFind, progress);
            if (template.PointFind.ResultX != 0 || template.PointFind.ResultY != 0)
                DrawPointResult(drawMat, template.PointFind);
        }

        var bmp = drawMat.ToBitmapSource();
        bmp.Freeze();
        return bmp;
    }

    // ==================== 执行逻辑 ====================

    private static void ExecuteNcc(Mat fullImage, NccConfig cfg, Action<string, bool>? progress)
    {
        try
        {
            if (!cfg.TemplateRoi.IsValid || !cfg.SearchRoi.IsValid)
            {
                progress?.Invoke("NCC: ROI 未设置", false);
                return;
            }

            var tRoi = ToRect(cfg.TemplateRoi);
            using var templateMat = new Mat(fullImage, tRoi);

            var sRoi = ToRect(cfg.SearchRoi);
            using var searchMat = new Mat(fullImage, sRoi);
         
            var param = new MatcherParam
            {
                ScoreThreshold = cfg.ScoreThreshold,
                Angle = cfg.SearchAngle,
                MaxCount = cfg.MaxCount,
                MinArea = cfg.MinArea,
                IouThreshold = cfg.IouThreshold,
            };

            using var matcher = new PatternMatcher(param);
            if (!matcher.SetTemplate(templateMat))
            {
                progress?.Invoke("NCC: 模板学习失败", false);
                return;
            }

            int count = matcher.Match(searchMat, out var results);
            if (count > 0 && results.Count > 0)
            {
                var best = results[0];
                cfg.ResultX = best.Center.X + cfg.SearchRoi.X;
                cfg.ResultY = best.Center.Y + cfg.SearchRoi.Y;
                cfg.ResultAngle = best.Angle;
                cfg.ResultScore = best.Score;
                cfg.Result = best;
                progress?.Invoke($"NCC: 匹配成功 (分数={best.Score:F3})", true);
            }
            else
            {
                progress?.Invoke("NCC: 未找到匹配", false);
            }
        }
        catch (Exception ex)
        {
            progress?.Invoke($"NCC: 执行异常 - {ex.Message}", false);
        }
    }

    private static void ExecuteEdgeFind(Mat fullImage, EdgeFindConfig cfg, string label, Action<string, bool>? progress)
    {
        try
        {
            if (!cfg.SearchRoi.IsValid)
            {
                progress?.Invoke($"{label}: ROI 未设置", false);
                return;
            }

            // 按 ROI 角度旋转裁剪，得到与 ROI 对齐的轴正图像
            using var roiMat = CropRotatedRoi(fullImage, cfg.SearchRoi);

            // 边缘方向角按图像坐标系解释，裁剪旋转后需换算到 ROI 坐标系
            double edgeAngleInRoi = cfg.EdgeDirectionDeg - cfg.SearchRoi.Angle;

            var result = CaliperEdgeFinder.Detect(
                roiMat,
                edgeAngleDeg: edgeAngleInRoi,
                caliperCount: cfg.CaliperCount,
                caliperWidth: cfg.CaliperWidth,
                searchHalf: cfg.SearchHalf,
                inlierThreshold: cfg.InlierThreshold);

            if (result.InlierCount < 2)
            {
                progress?.Invoke($"{label}: 未找到有效边缘", false);
                return;
            }

            // 裁剪坐标系 → 原图坐标系（旋转还原）
            var start = RotatedRoiToImage(cfg.SearchRoi, result.Start.X, result.Start.Y);
            var end = RotatedRoiToImage(cfg.SearchRoi, result.End.X, result.End.Y);

            cfg.ResultStartX = start.X;
            cfg.ResultStartY = start.Y;
            cfg.ResultEndX = end.X;
            cfg.ResultEndY = end.Y;
            cfg.ResultAngleDeg = result.AngleDeg + cfg.SearchRoi.Angle;

            progress?.Invoke($"{label}: 找到边缘 (内点数={result.InlierCount})", true);
        }
        catch (Exception ex)
        {
            progress?.Invoke($"{label}: 执行异常 - {ex.Message}", false);
        }
    }

    private static void ExecutePointFind(EdgeFindConfig edge1, EdgeFindConfig edge2, PointFindConfig cfg, Action<string, bool>? progress)
    {
        try
        {
            var p1Start = new PointF((float)edge1.ResultStartX, (float)edge1.ResultStartY);
            var p1End = new PointF((float)edge1.ResultEndX, (float)edge1.ResultEndY);
            var p2Start = new PointF((float)edge2.ResultStartX, (float)edge2.ResultStartY);
            var p2End = new PointF((float)edge2.ResultEndX, (float)edge2.ResultEndY);

            if (LineIntersection(p1Start, p1End, p2Start, p2End, out var intersection))
            {
                cfg.ResultX = intersection.X;
                cfg.ResultY = intersection.Y;
                progress?.Invoke($"找点: 交点=({intersection.X:F1}, {intersection.Y:F1})", true);
            }
            else
            {
                progress?.Invoke("找点: 两线平行或未找到交点", false);
            }
        }
        catch (Exception ex)
        {
            progress?.Invoke($"找点: 执行异常 - {ex.Message}", false);
        }
    }

    // ==================== 绘图逻辑（在彩色 clone 上画） ====================

    private static void DrawNccResult(Mat draw, NccConfig cfg)
    {
        var tw = cfg.TemplateRoi.Width;
        var th = cfg.TemplateRoi.Height;
        var cx = (float)cfg.ResultX;
        var cy = (float)cfg.ResultY;

        var rect = new RotatedRect(
            new PointF(cx, cy),
            new SizeF((float)tw, (float)th),
            (float)cfg.ResultAngle);
        var pts = rect.GetVertices();
        var red = new Bgr(0,0, 255).MCvScalar;
        for (int i = 0; i < 4; i++)
            CvInvoke.Line(draw,
                new((int)pts[i].X, (int)pts[i].Y),
                new((int)pts[(i + 1) % 4].X, (int)pts[(i + 1) % 4].Y),
                red, 5);

        // 中心十字
        DrawCross(draw, cx, cy, 50, red);

    }

    private static void DrawEdgeResult(Mat draw, EdgeFindConfig cfg)
    {
        if (cfg.ResultStartX == 0 && cfg.ResultStartY == 0 &&
            cfg.ResultEndX == 0 && cfg.ResultEndY == 0)
            return;

        var blue = new Bgr(0, 255, 0).MCvScalar;
        CvInvoke.Line(draw,
            new((int)cfg.ResultStartX, (int)cfg.ResultStartY),
            new((int)cfg.ResultEndX, (int)cfg.ResultEndY),
            blue, 5);
    }

    private static void DrawPointResult(Mat draw, PointFindConfig cfg)
    {
        var red = new Bgr(255, 0, 255).MCvScalar;
        var pt = new Point((int)cfg.ResultX, (int)cfg.ResultY);
        CvInvoke.Circle(draw, pt, 10, red, -1);
        DrawCross(draw, cfg.ResultX, cfg.ResultY, 12, red);
    }

    private static void DrawCross(Mat draw, double cx, double cy, int size, Emgu.CV.Structure.MCvScalar color)
    {
        var x = (int)cx;
        var y = (int)cy;
        CvInvoke.Line(draw, new(x - size, y), new(x + size, y), color, 5);
        CvInvoke.Line(draw, new(x, y - size), new(x, y + size), color, 5);
    }

    // ==================== 几何工具 ====================

    private static bool LineIntersection(PointF p1, PointF p2, PointF p3, PointF p4, out PointF intersection)
    {
        intersection = PointF.Empty;

        float x1 = p1.X, y1 = p1.Y, x2 = p2.X, y2 = p2.Y;
        float x3 = p3.X, y3 = p3.Y, x4 = p4.X, y4 = p4.Y;

        float denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < 1e-10f) return false;

        float t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
        intersection = new PointF(
            x1 + t * (x2 - x1),
            y1 + t * (y2 - y1));
        return true;
    }

    internal static Rectangle ToRect(RoiData roi) =>
        new((int)roi.X, (int)roi.Y, (int)roi.Width, (int)roi.Height);

    /// <summary>
    /// 按 ROI 角度旋转裁剪出与 ROI 对齐的轴正图像。
    /// 角度为 0 时退化为普通矩形裁剪，行为与原实现一致。
    /// </summary>
    internal static Mat CropRotatedRoi(Mat fullImage, RoiData roi)
    {
        if (roi.Angle == 0)
            return new Mat(fullImage, ToRect(roi));

        double rad = roi.Angle * Math.PI / 180;
        double cosA = Math.Cos(rad), sinA = Math.Sin(rad);
        double cx = roi.X + roi.Width / 2;
        double cy = roi.Y + roi.Height / 2;
        double w = roi.Width, h = roi.Height;

        // 仿射矩阵：将 ROI 中心置为坐标原点，按 -Angle 反向旋转使内容轴对齐，
        // 平移使 ROI 左上角映射到输出 (0,0)
        using (var M = new Mat(2, 3, DepthType.Cv64F, 1))
        {
            double[] m =
            {
                cosA, -sinA, cx - w / 2 * cosA + h / 2 * sinA,
                sinA,  cosA, cy - w / 2 * sinA - h / 2 * cosA,
            };
            Marshal.Copy(m, 0, M.DataPointer, 6);

            var dst = new Mat();
            CvInvoke.WarpAffine(fullImage, dst, M,
                new Size((int)Math.Round(w), (int)Math.Round(h)),
                Inter.Linear, Warp.Default, BorderType.Constant, new MCvScalar(0));
            return dst;
        }
    }

    /// <summary>旋转 ROI 裁剪坐标系坐标 → 原图坐标（旋转还原 + 平移）</summary>
    internal static (double X, double Y) RotatedRoiToImage(RoiData roi, double cropX, double cropY)
    {
        double rad = roi.Angle * Math.PI / 180;
        double cosA = Math.Cos(rad), sinA = Math.Sin(rad);
        double cx = roi.X + roi.Width / 2;
        double cy = roi.Y + roi.Height / 2;
        double dx = cropX - roi.Width / 2;
        double dy = cropY - roi.Height / 2;
        return (cx + dx * cosA - dy * sinA, cy + dx * sinA + dy * cosA);
    }
}
