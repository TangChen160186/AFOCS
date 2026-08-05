using AFOCS.VisionEditor.Models;
using Emgu.CV;
using Emgu.CV.CvEnum;
using System.Drawing;
using VisionToolkit.EdgeFinder;
using VisionToolkit.TemplateMatcher;

namespace AFOCS.VisionEditor.Services;

/// <summary>
/// 视觉流水线执行服务 —— 按 NCC → 找边1 → 找边2 → 找点 顺序执行
/// </summary>
public class VisionExecutionService
{
    /// <summary>
    /// 执行完整流水线，结果直接写回 template 的对应 Config 中
    /// </summary>
    /// <param name="imagePath">图片完整路径</param>
    /// <param name="template">视觉模板（结果会写回）</param>
    /// <param name="progress">进度回调：arg1=步骤描述, arg2=是否成功</param>
    public void Execute(string imagePath, VisionTemplate template, Action<string, bool>? progress = null)
    {
        using var image = CvInvoke.Imread(imagePath, ImreadModes.Grayscale);
        if (image == null || image.IsEmpty)
        {
            progress?.Invoke("无法加载图片", false);
            return;
        }

        // Step 1: NCC
        if (template.Ncc.IsEnabled)
            ExecuteNcc(image, template.Ncc, progress);

        // Step 2: 找边 1
        if (template.EdgeFind1.IsEnabled)
            ExecuteEdgeFind(image, template.EdgeFind1, "找边1", progress);

        // Step 3: 找边 2
        if (template.EdgeFind2.IsEnabled)
            ExecuteEdgeFind(image, template.EdgeFind2, "找边2", progress);

        // Step 4: 找点（Edge1 × Edge2 交点）
        if (template.PointFind.IsEnabled)
            ExecutePointFind(template.EdgeFind1, template.EdgeFind2, template.PointFind, progress);
    }

    // ==================== NCC ====================

    private static void ExecuteNcc(Mat fullImage, NccConfig cfg, Action<string, bool>? progress)
    {
        try
        {
            if (!cfg.TemplateRoi.IsValid || !cfg.SearchRoi.IsValid)
            {
                progress?.Invoke("NCC: ROI 未设置", false);
                return;
            }

            // 提取模板图像
            var tRoi = ToRect(cfg.TemplateRoi);
            using var templateMat = new Mat(fullImage, tRoi);

            // 提取搜索区域
            var sRoi = ToRect(cfg.SearchRoi);
            using var searchMat = new Mat(fullImage, sRoi);

            // 创建匹配器并训练
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
                // 结果坐标转换回全图坐标系（SearchRoi 偏移）
                cfg.ResultX = best.Center.X + cfg.SearchRoi.X;
                cfg.ResultY = best.Center.Y + cfg.SearchRoi.Y;
                cfg.ResultAngle = best.Angle;
                cfg.ResultScore = best.Score;
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

    // ==================== 找边 ====================

    private static void ExecuteEdgeFind(Mat fullImage, EdgeFindConfig cfg, string label, Action<string, bool>? progress)
    {
        try
        {
            if (!cfg.SearchRoi.IsValid)
            {
                progress?.Invoke($"{label}: ROI 未设置", false);
                return;
            }

            var roi = ToRect(cfg.SearchRoi);
            using var roiMat = new Mat(fullImage, roi);

            var result = CaliperEdgeFinder.Detect(
                roiMat,
                edgeAngleDeg: cfg.EdgeDirectionDeg,
                caliperCount: cfg.CaliperCount,
                caliperWidth: cfg.CaliperWidth,
                searchHalf: cfg.SearchHalf,
                inlierThreshold: cfg.InlierThreshold);

            if (result.InlierCount < 2)
            {
                progress?.Invoke($"{label}: 未找到有效边缘", false);
                return;
            }

            // 坐标转换回全图坐标系
            cfg.ResultStartX = result.Start.X + cfg.SearchRoi.X;
            cfg.ResultStartY = result.Start.Y + cfg.SearchRoi.Y;
            cfg.ResultEndX = result.End.X + cfg.SearchRoi.X;
            cfg.ResultEndY = result.End.Y + cfg.SearchRoi.Y;
            cfg.ResultAngleDeg = result.AngleDeg;

            progress?.Invoke($"{label}: 找到边缘 (内点数={result.InlierCount})", true);
        }
        catch (Exception ex)
        {
            progress?.Invoke($"{label}: 执行异常 - {ex.Message}", false);
        }
    }

    // ==================== 找点（两线交点） ====================

    private static void ExecutePointFind(EdgeFindConfig edge1, EdgeFindConfig edge2, PointFindConfig cfg, Action<string, bool>? progress)
    {
        try
        {
            // 计算 Edge1 与 Edge2 的交点
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

    // ==================== 几何工具 ====================

    /// <summary>计算两条线段的交点（无限延伸）</summary>
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

    private static Rectangle ToRect(RoiData roi) =>
        new((int)roi.X, (int)roi.Y, (int)roi.Width, (int)roi.Height);
}
