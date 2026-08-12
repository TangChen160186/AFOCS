using System.IO;
using AFOCS.VisionEditor.Models;
using HalconDotNet;

namespace AFOCS.VisionEditor.Services;

/// <summary>
/// 视觉检测服务：传入 HImage，基于模板结果计算各流程的偏差量。
/// 流程：NCC(计算偏移dx,dy) → 偏移后的找边1/2(角度偏差) → 找点(点偏差)
/// NCC 使用 Halcon ReadShapeModel/FindShapeModel，找边使用 Halcon HMetrologyModel。
/// </summary>
public class VisionInspectionService
{
    /// <summary>
    /// 对新图像执行检测，返回各流程的偏差结果。
    /// </summary>
    /// <param name="hImage">Halcon 图像</param>
    /// <param name="template">已通过 VisionExecutionService 执行过模板计算的 VisionTemplate</param>
    public VisionInspectionResult? Inspect(HImage hImage, VisionTemplate template)
    {
        if (hImage == null)
            return null;

        var result = new VisionInspectionResult();
        double dx = 0, dy = 0;

        // Step 1: NCC → 计算匹配偏移
        if (template.Ncc.IsEnabled)
        {
            result.NccSuccess = ExecuteNccInspectionHalcon(
                hImage, template.Ncc,
                out dx, out dy, out var matchedAngle,
                out var newCx, out var newCy);
            result.Dx = dx;
            result.Dy = dy;
            result.NccResultColumn = newCx;
            result.NccResultRow = newCy;
            result.NccResultAngle = matchedAngle;
        }

        double e1StartX = 0, e1StartY = 0, e1EndX = 0, e1EndY = 0;
        double e2StartX = 0, e2StartY = 0, e2EndX = 0, e2EndY = 0;

        // Step 2: 找边1（Halcon HMetrologyModel，ROI 用 NCC 偏移补偿）
        if (template.EdgeFind1.IsEnabled)
        {
            result.Edge1Success = ExecuteEdgeInspectionHalcon(
                hImage, template.EdgeFind1, dx, dy,
                out var edge1Dev,
                out e1StartX, out e1StartY,
                out e1EndX, out e1EndY);
            result.Edge1AngleDev = edge1Dev;
            result.Edge1ResultStartX = e1StartX;
            result.Edge1ResultStartY = e1StartY;
            result.Edge1ResultEndX = e1EndX;
            result.Edge1ResultEndY = e1EndY;
        }

        // Step 3: 找边2
        if (template.EdgeFind2.IsEnabled)
        {
            result.Edge2Success = ExecuteEdgeInspectionHalcon(
                hImage, template.EdgeFind2, dx, dy,
                out var edge2Dev,
                out e2StartX, out e2StartY,
                out e2EndX, out e2EndY);
            result.Edge2AngleDev = edge2Dev;
            result.Edge2ResultStartX = e2StartX;
            result.Edge2ResultStartY = e2StartY;
            result.Edge2ResultEndX = e2EndX;
            result.Edge2ResultEndY = e2EndY;
        }

        // Step 4: 找点
        if (template.PointFind.IsEnabled && result.Edge1Success && result.Edge2Success)
        {
            result.PointSuccess = ExecutePointInspection(
                e1StartX, e1StartY, e1EndX, e1EndY,
                e2StartX, e2StartY, e2EndX, e2EndY,
                template.PointFind.ResultX, template.PointFind.ResultY,
                out var devX, out var devY,
                out var newPx, out var newPy);
            result.PointDevX = devX;
            result.PointDevY = devY;
            result.PointResultX = newPx;
            result.PointResultY = newPy;
        }

        return result;
    }

    // ==================== NCC：Halcon ReadShapeModel/FindShapeModel ====================

    private static bool ExecuteNccInspectionHalcon(
        HImage hImage, NccConfig cfg,
        out double dx, out double dy, out double matchedAngle,
        out double newCx, out double newCy)
    {
        dx = dy = 0;
        matchedAngle = 0;
        newCx = newCy = 0;
        try
        {
            if (string.IsNullOrEmpty(cfg.ModelPath) || !File.Exists(cfg.ModelPath))
                return false;

            HOperatorSet.ReadShapeModel(cfg.ModelPath, out HTuple modelId);

            HOperatorSet.FindShapeModel(hImage, modelId,
                new HTuple(0).TupleRad(), new HTuple(360).TupleRad(),
                cfg.MinScore, 1, 0.5, "least_squares",
                new HTuple(4).TupleConcat(1), 0.4,
                out HTuple hvRow, out HTuple hvColumn, out HTuple hvAngle, out HTuple hvScore);

            if (hvScore.Length > 0 && hvScore[0].D > 0)
            {
                newCx = hvColumn[0].D;
                newCy = hvRow[0].D;
                matchedAngle = hvAngle[0].D * 180.0 / Math.PI;
                dx = newCx - cfg.ResultX;
                dy = newCy - cfg.ResultY;
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    // ==================== 找边 ====================

    private static bool ExecuteEdgeInspectionHalcon(
        HImage hImage, EdgeFindConfig cfg, double dx, double dy,
        out double angleDev,
        out double startX, out double startY,
        out double endX, out double endY)
    {
        angleDev = 0;
        startX = startY = endX = endY = 0;
        try
        {
            double r1 = cfg.Row1 + dy;
            double c1 = cfg.Col1 + dx;
            double r2 = cfg.Row2 + dy;
            double c2 = cfg.Col2 + dx;

            hImage.GetImageSize(out int width, out int height);

            using var metrologyModel = new HMetrologyModel();
            metrologyModel.SetMetrologyModelImageSize(width, height);

            metrologyModel.AddMetrologyObjectLineMeasure(
                r1, c1, r2, c2,
                cfg.MeasureLength1, cfg.MeasureLength2,
                cfg.MeasureSigma, cfg.MeasureThreshold,
                new HTuple(), new HTuple());

            metrologyModel.ApplyMetrologyModel(hImage);

            HTuple lineRet = metrologyModel.GetMetrologyObjectResult(
                "all", "all", "result_type", "all_param");

            double[] retAry = lineRet.DArr;
            if (retAry.Length < 4) return false;

            startX = retAry[1];
            startY = retAry[0];
            endX = retAry[3];
            endY = retAry[2];

            HOperatorSet.AngleLx(endY, endX, startY, startX, out HTuple angle);
            double newAngle = angle[0].D * 180.0 / Math.PI;
            angleDev = NormalizeAngle(newAngle - cfg.ResultAngleDeg);

            return true;
        }
        catch
        {
            return false;
        }
    }

    // ==================== 找点 ====================

    private static bool ExecutePointInspection(
        double e1StartX, double e1StartY, double e1EndX, double e1EndY,
        double e2StartX, double e2StartY, double e2EndX, double e2EndY,
        double templatePointX, double templatePointY,
        out double devX, out double devY,
        out double newPx, out double newPy)
    {
        devX = devY = 0;
        newPx = newPy = 0;
        try
        {
            if (!LineIntersection(
                    e1StartX, e1StartY, e1EndX, e1EndY,
                    e2StartX, e2StartY, e2EndX, e2EndY,
                    out newPx, out newPy))
                return false;

            devX = newPx - templatePointX;
            devY = newPy - templatePointY;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ==================== 工具方法 ====================

    private static double NormalizeAngle(double deg)
    {
        deg %= 360;
        if (deg > 180) deg -= 360;
        if (deg < -180) deg += 360;
        return deg;
    }

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

/// <summary>
/// 视觉检测偏差结果。各字段仅在对应流程启用且成功时有效。
/// </summary>
public class VisionInspectionResult
{
    public bool NccSuccess { get; set; }
    public double Dx { get; set; }
    public double Dy { get; set; }

    // NCC 绘制用：匹配到的位置
    public double NccResultRow { get; set; }
    public double NccResultColumn { get; set; }
    public double NccResultAngle { get; set; }

    public bool Edge1Success { get; set; }
    public double Edge1AngleDev { get; set; }
    // 找边1 绘制用：结果线段端点
    public double Edge1ResultStartX { get; set; }
    public double Edge1ResultStartY { get; set; }
    public double Edge1ResultEndX { get; set; }
    public double Edge1ResultEndY { get; set; }

    public bool Edge2Success { get; set; }
    public double Edge2AngleDev { get; set; }
    // 找边2 绘制用：结果线段端点
    public double Edge2ResultStartX { get; set; }
    public double Edge2ResultStartY { get; set; }
    public double Edge2ResultEndX { get; set; }
    public double Edge2ResultEndY { get; set; }

    public bool PointSuccess { get; set; }
    public double PointDevX { get; set; }
    public double PointDevY { get; set; }
    // 找点 绘制用：新交点坐标
    public double PointResultX { get; set; }
    public double PointResultY { get; set; }
}
