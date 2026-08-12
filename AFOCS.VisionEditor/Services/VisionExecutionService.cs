using System.IO;
using AFOCS.VisionEditor.Models;
using HalconDotNet;

namespace AFOCS.VisionEditor.Services;

/// <summary>
/// 视觉流水线执行服务 —— 按 NCC → 找边1 → 找边2 → 找点 顺序执行。
/// NCC 使用 Halcon CreateShapeModel/FindShapeModel，找边使用 Halcon HMetrologyModel。
/// </summary>
public class VisionExecutionService
{
    /// <summary>
    /// 执行完整流水线，结果写回 template。返回是否成功。
    /// </summary>
    public bool Execute(string imagePath, VisionTemplate template, Action<string, bool>? progress = null)
    {
        var allOk = true;

        // Step 1: NCC（Halcon ShapeModel）
        if (template.Ncc.IsEnabled)
        {
            var ok = ExecuteNccHalcon(imagePath, template.Ncc, progress);
            if (!ok) allOk = false;
        }

        // Step 2: 找边 1
        if (template.EdgeFind1.IsEnabled)
        {
            var ok = ExecuteEdgeFindHalcon(imagePath, template.EdgeFind1, "找边1", progress);
            if (!ok) allOk = false;
        }

        // Step 3: 找边 2
        if (template.EdgeFind2.IsEnabled)
        {
            var ok = ExecuteEdgeFindHalcon(imagePath, template.EdgeFind2, "找边2", progress);
            if (!ok) allOk = false;
        }

        // Step 4: 找点
        if (template.PointFind.IsEnabled)
        {
            var ok = ExecutePointFind(template.EdgeFind1, template.EdgeFind2, template.PointFind, progress);
            if (!ok) allOk = false;
        }

        return allOk;
    }

    // ==================== NCC：Halcon ShapeModel ====================

    private static bool ExecuteNccHalcon(string imagePath, NccConfig cfg, Action<string, bool>? progress)
    {
        try
        {
            using var image = new HImage(imagePath);

            // 1. 创建旋转矩形 ROI 并缩小区域
            HOperatorSet.GenRectangle2(out HObject roiRect,
                cfg.Row, cfg.Column, cfg.Phi, cfg.Length1, cfg.Length2);
            HOperatorSet.ReduceDomain(image, roiRect, out HObject imageReduced);

            // 2. 创建形状模板
            HOperatorSet.CreateShapeModel(imageReduced, "auto",
                new HTuple(0).TupleRad(), new HTuple(360).TupleRad(),
                "auto", "auto", "use_polarity", "auto", "auto",
                out HTuple modelId);

            // 3. 保存模型到 .shm 文件（与图片同目录）
            string modelDir = Path.GetDirectoryName(imagePath) ?? ".";
            string modelFile = Path.GetFileNameWithoutExtension(imagePath) + ".shm";
            string modelAbsPath = Path.Combine(modelDir, modelFile);
            HOperatorSet.WriteShapeModel(modelId, modelAbsPath);
            cfg.ModelPath = modelAbsPath;

            // 4. 在全图上搜索
            HOperatorSet.FindShapeModel(image, modelId,
                new HTuple(0).TupleRad(), new HTuple(360).TupleRad(),
                cfg.MinScore, 1, 0.5, "least_squares",
                new HTuple(4).TupleConcat(1), 0.4,
                out HTuple hvRow, out HTuple hvColumn, out HTuple hvAngle, out HTuple hvScore);

            if (hvScore.Length > 0 && hvScore[0].D > 0)
            {
                cfg.ResultX = hvColumn[0].D;
                cfg.ResultY = hvRow[0].D;
                cfg.ResultAngle = hvAngle[0].D * 180.0 / Math.PI;
                cfg.ResultScore = hvScore[0].D;
                progress?.Invoke($"NCC: 匹配成功 (分数={hvScore[0].D:F3})", true);
                return true;
            }

            progress?.Invoke("NCC: 未找到匹配", false);
            return false;
        }
        catch (Exception ex)
        {
            progress?.Invoke($"NCC: 执行异常 - {ex.Message}", false);
            return false;
        }
    }

    // ==================== 找边 ====================

    private static bool ExecuteEdgeFindHalcon(string imagePath, EdgeFindConfig cfg, string label, Action<string, bool>? progress)
    {
        try
        {
            using var image = new HImage(imagePath);
            image.GetImageSize(out int width, out int height);

            using var metrologyModel = new HMetrologyModel();
            metrologyModel.SetMetrologyModelImageSize(width, height);

            metrologyModel.AddMetrologyObjectLineMeasure(
                cfg.Row1, cfg.Col1, cfg.Row2, cfg.Col2,
                cfg.MeasureLength1, cfg.MeasureLength2,
                cfg.MeasureSigma, cfg.MeasureThreshold,
                new HTuple(), new HTuple());

            metrologyModel.ApplyMetrologyModel(image);

            HTuple lineRet = metrologyModel.GetMetrologyObjectResult(
                "all", "all", "result_type", "all_param");

            double[] retAry = lineRet.DArr;

            cfg.ResultStartX = retAry[1]; // column_begin
            cfg.ResultStartY = retAry[0]; // row_begin
            cfg.ResultEndX = retAry[3];   // column_end
            cfg.ResultEndY = retAry[2];   // row_end

            HOperatorSet.AngleLx(
                cfg.ResultEndY, cfg.ResultEndX,
                cfg.ResultStartY, cfg.ResultStartX,
                out HTuple angle);
            cfg.ResultAngleDeg = angle[0].D * 180.0 / Math.PI;

            progress?.Invoke($"{label}: 找到边缘 (角度={cfg.ResultAngleDeg:F2}°)", true);
            return true;
        }
        catch (Exception ex)
        {
            progress?.Invoke($"{label}: 执行异常 - {ex.Message}", false);
            return false;
        }
    }

    // ==================== 找点（两线交点） ====================

    private static bool ExecutePointFind(EdgeFindConfig edge1, EdgeFindConfig edge2, PointFindConfig cfg, Action<string, bool>? progress)
    {
        try
        {
            if (!LineIntersection(
                    edge1.ResultStartX, edge1.ResultStartY, edge1.ResultEndX, edge1.ResultEndY,
                    edge2.ResultStartX, edge2.ResultStartY, edge2.ResultEndX, edge2.ResultEndY,
                    out var ix, out var iy))
            {
                progress?.Invoke("找点: 两线平行或未找到交点", false);
                return false;
            }

            cfg.ResultX = ix;
            cfg.ResultY = iy;
            progress?.Invoke($"找点: 交点=({ix:F1}, {iy:F1})", true);
            return true;
        }
        catch (Exception ex)
        {
            progress?.Invoke($"找点: 执行异常 - {ex.Message}", false);
            return false;
        }
    }

    // ==================== 几何工具 ====================

    private static bool LineIntersection(
        double x1, double y1, double x2, double y2,
        double x3, double y3, double x4, double y4,
        out double ix, out double iy)
    {
        ix = iy = 0;
        double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < 1e-10) return false;
        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
        ix = x1 + t * (x2 - x1);
        iy = y1 + t * (y2 - y1);
        return true;
    }
}
