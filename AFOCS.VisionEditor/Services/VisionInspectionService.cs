using System.Drawing;
using AFOCS.VisionEditor.Models;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using VisionToolkit.EdgeFinder;
using VisionToolkit.TemplateMatcher;

namespace AFOCS.VisionEditor.Services;

/// <summary>
/// 视觉检测服务：传入新图像 Mat，基于模板结果计算各流程的偏差量。
/// 流程：NCC(计算偏移dx,dy) → 偏移后的找边1/2(角度偏差) → 找点(点偏差)
/// </summary>
public class VisionInspectionService
{
    /// <summary>
    /// 对新图像执行检测，返回各流程的偏差结果。
    /// </summary>
    /// <param name="grayImage">新图像的灰度 Mat（8UC1）</param>
    /// <param name="colorImage">新图像的彩色 Mat（8UC3），传入时 result.DrawMat 包含绘制结果</param>
    /// <param name="template">已通过 VisionExecutionService 执行过模板计算的 VisionTemplate</param>
    public VisionInspectionResult? Inspect(Mat grayImage, Mat? colorImage, VisionTemplate template)
    {
        if (grayImage == null || grayImage.IsEmpty)
            return null;

        var result = new VisionInspectionResult();
        double dx = 0, dy = 0;

        // 克隆彩色图用于绘制
        Mat? drawMat = null;
        if (colorImage != null && !colorImage.IsEmpty)
        {
            drawMat = colorImage.Clone();
            result.DrawMat = drawMat;
        }

        // Step 1: NCC → 计算匹配偏移
        if (template.Ncc.IsEnabled)
        {
            result.NccSuccess = ExecuteNccInspection(grayImage, template.Ncc, out dx, out dy, out var matchedAngle, out var newCx, out var newCy);
            result.Dx = dx;
            result.Dy = dy;

            if (result.NccSuccess && drawMat != null)
                DrawNcc(drawMat, template.Ncc, newCx, newCy, matchedAngle);
        }

        double e1StartX = 0, e1StartY = 0, e1EndX = 0, e1EndY = 0;
        double e2StartX = 0, e2StartY = 0, e2EndX = 0, e2EndY = 0;

        // Step 2: 找边1（ROI 用 NCC 偏移补偿）
        if (template.EdgeFind1.IsEnabled)
        {
            var shiftedRoi = ShiftRoi(template.EdgeFind1.SearchRoi, dx, dy);
            result.Edge1Success = ExecuteEdgeInspection(
                grayImage, shiftedRoi,
                template.EdgeFind1.EdgeDirectionDeg,
                template.EdgeFind1.CaliperCount,
                template.EdgeFind1.CaliperWidth,
                template.EdgeFind1.SearchHalf,
                template.EdgeFind1.InlierThreshold,
                template.EdgeFind1.ResultAngleDeg,
                out var edge1Dev,
                out e1StartX, out e1StartY,
                out e1EndX, out e1EndY);
            result.Edge1AngleDev = edge1Dev;

            if (result.Edge1Success && drawMat != null)
                DrawEdge(drawMat, e1StartX, e1StartY, e1EndX, e1EndY, new Bgr(255, 0, 0).MCvScalar);
        }

        // Step 3: 找边2（ROI 用 NCC 偏移补偿）
        if (template.EdgeFind2.IsEnabled)
        {
            var shiftedRoi = ShiftRoi(template.EdgeFind2.SearchRoi, dx, dy);
            double edge2Dev;
            result.Edge2Success = ExecuteEdgeInspection(
                grayImage, shiftedRoi,
                template.EdgeFind2.EdgeDirectionDeg,
                template.EdgeFind2.CaliperCount,
                template.EdgeFind2.CaliperWidth,
                template.EdgeFind2.SearchHalf,
                template.EdgeFind2.InlierThreshold,
                template.EdgeFind2.ResultAngleDeg,
                out edge2Dev,
                out e2StartX, out e2StartY,
                out e2EndX, out e2EndY);
            result.Edge2AngleDev = edge2Dev;

            if (result.Edge2Success && drawMat != null)
                DrawEdge(drawMat, e2StartX, e2StartY, e2EndX, e2EndY, new Bgr(0, 255, 0).MCvScalar);
        }

        // Step 4: 找点（计算新交点与模板交点的偏差）
        if (template.PointFind.IsEnabled && result.Edge1Success && result.Edge2Success)
        {
            result.PointSuccess = ExecutePointInspection(
                e1StartX, e1StartY, e1EndX, e1EndY,
                e2StartX, e2StartY, e2EndX, e2EndY,
                template.PointFind.ResultX, template.PointFind.ResultY,
                out var devX, out var devY, out var newPx, out var newPy);
            result.PointDevX = devX;
            result.PointDevY = devY;

            if (result.PointSuccess && drawMat != null)
                DrawPoint(drawMat, newPx, newPy);
        }

        return result;
    }

    // ==================== 各步骤实现 ====================

    /// <summary>在新图像上执行 NCC，返回匹配中心相对于模板中心的偏移 (dx, dy)、匹配角度和图像坐标下的新中心</summary>
    private static bool ExecuteNccInspection(Mat fullImage, NccConfig cfg,
        out double dx, out double dy, out double matchedAngle, out double newCx, out double newCy)
    {
        dx = dy = 0;
        matchedAngle = 0;
        newCx = newCy = 0;
        try
        {
            if (!cfg.TemplateRoi.IsValid || !cfg.SearchRoi.IsValid)
                return false;

            var tRoi = VisionExecutionService.ToRect(cfg.TemplateRoi);
            using var templateMat = new Mat(fullImage, tRoi);

            var sRoi = VisionExecutionService.ToRect(cfg.SearchRoi);
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
                return false;

            int count = matcher.Match(searchMat, out var results);
            if (count > 0 && results.Count > 0)
            {
                var best = results[0];
                newCx = best.Center.X + cfg.SearchRoi.X;
                newCy = best.Center.Y + cfg.SearchRoi.Y;
                matchedAngle = best.Angle;
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

    /// <summary>在新图像上执行找边（已应用 NCC 偏移的 ROI），返回角度偏差和图像坐标下的线段端点</summary>
    private static bool ExecuteEdgeInspection(
        Mat fullImage, RoiData roi,
        double edgeDirectionDeg, int caliperCount, double caliperWidth,
        double searchHalf, double inlierThreshold,
        double templateAngleDeg,
        out double angleDev,
        out double startX, out double startY,
        out double endX, out double endY)
    {
        angleDev = 0;
        startX = startY = endX = endY = 0;
        try
        {
            if (!roi.IsValid) return false;

            using var roiMat = VisionExecutionService.CropRotatedRoi(fullImage, roi);
            double edgeAngleInRoi = edgeDirectionDeg - roi.Angle;

            var edgeResult = CaliperEdgeFinder.Detect(
                roiMat,
                edgeAngleDeg: edgeAngleInRoi,
                caliperCount,
                caliperWidth,
                searchHalf,
                inlierThreshold);

            if (edgeResult.InlierCount < 2) return false;

            // 裁剪坐标 → 图像坐标
            var imgStart = VisionExecutionService.RotatedRoiToImage(roi, edgeResult.Start.X, edgeResult.Start.Y);
            var imgEnd = VisionExecutionService.RotatedRoiToImage(roi, edgeResult.End.X, edgeResult.End.Y);
            startX = imgStart.X;
            startY = imgStart.Y;
            endX = imgEnd.X;
            endY = imgEnd.Y;

            double newAngle = edgeResult.AngleDeg + roi.Angle;
            angleDev = NormalizeAngle(newAngle - templateAngleDeg);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>计算新交点与模板交点的偏差，以及新交点坐标</summary>
    private static bool ExecutePointInspection(
        double e1StartX, double e1StartY, double e1EndX, double e1EndY,
        double e2StartX, double e2StartY, double e2EndX, double e2EndY,
        double templatePointX, double templatePointY,
        out double devX, out double devY, out double newPx, out double newPy)
    {
        devX = devY = 0;
        newPx = newPy = 0;
        try
        {
            var p1Start = new PointF((float)e1StartX, (float)e1StartY);
            var p1End = new PointF((float)e1EndX, (float)e1EndY);
            var p2Start = new PointF((float)e2StartX, (float)e2StartY);
            var p2End = new PointF((float)e2EndX, (float)e2EndY);

            if (!LineIntersection(p1Start, p1End, p2Start, p2End, out var intersection))
                return false;

            newPx = intersection.X;
            newPy = intersection.Y;
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

    /// <summary>ROI 平移（不改变宽高和角度）</summary>
    private static RoiData ShiftRoi(RoiData src, double dx, double dy) => new()
    {
        X = src.X + dx,
        Y = src.Y + dy,
        Width = src.Width,
        Height = src.Height,
        Angle = src.Angle,
    };

    /// <summary>角度归一化到 [-180, 180]</summary>
    private static double NormalizeAngle(double deg)
    {
        deg %= 360;
        if (deg > 180) deg -= 360;
        if (deg < -180) deg += 360;
        return deg;
    }

    private static bool LineIntersection(PointF p1, PointF p2, PointF p3, PointF p4, out PointF intersection)
    {
        intersection = PointF.Empty;
        float x1 = p1.X, y1 = p1.Y, x2 = p2.X, y2 = p2.Y;
        float x3 = p3.X, y3 = p3.Y, x4 = p4.X, y4 = p4.Y;
        float denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < 1e-10f) return false;
        float t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
        intersection = new PointF(x1 + t * (x2 - x1), y1 + t * (y2 - y1));
        return true;
    }

    // ==================== 绘图方法 ====================

    private static void DrawNcc(Mat draw, NccConfig cfg, double cx, double cy, double angle)
    {
        var tw = cfg.TemplateRoi.Width;
        var th = cfg.TemplateRoi.Height;
        var rect = new RotatedRect(
            new PointF((float)cx, (float)cy),
            new SizeF((float)tw, (float)th),
            (float)angle);
        var pts = rect.GetVertices();
        var red = new Bgr(0, 0, 255).MCvScalar;
        for (int i = 0; i < 4; i++)
            CvInvoke.Line(draw,
                new((int)pts[i].X, (int)pts[i].Y),
                new((int)pts[(i + 1) % 4].X, (int)pts[(i + 1) % 4].Y),
                red, 5);
        DrawCross(draw, cx, cy, 50, red);
    }

    private static void DrawEdge(Mat draw, double startX, double startY, double endX, double endY, MCvScalar color)
    {
        CvInvoke.Line(draw,
            new((int)startX, (int)startY),
            new((int)endX, (int)endY),
            color, 5);
    }

    private static void DrawPoint(Mat draw, double x, double y)
    {
        var magenta = new Bgr(255, 0, 255).MCvScalar;
        CvInvoke.Circle(draw, new((int)x, (int)y), 10, magenta, -1);
        DrawCross(draw, x, y, 12, magenta);
    }

    private static void DrawCross(Mat draw, double cx, double cy, int size, MCvScalar color)
    {
        var x = (int)cx;
        var y = (int)cy;
        CvInvoke.Line(draw, new(x - size, y), new(x + size, y), color, 5);
        CvInvoke.Line(draw, new(x, y - size), new(x, y + size), color, 5);
    }
}

/// <summary>
/// 视觉检测偏差结果。各字段仅在对应流程启用且成功时有效。
/// </summary>
public class VisionInspectionResult
{
    /// <summary>NCC 匹配是否成功</summary>
    public bool NccSuccess { get; set; }
    /// <summary>NCC 匹配中心 X 偏差（像素），新中心 = 模板中心 + Dx</summary>
    public double Dx { get; set; }
    /// <summary>NCC 匹配中心 Y 偏差（像素），新中心 = 模板中心 + Dy</summary>
    public double Dy { get; set; }

    /// <summary>找边1 是否成功</summary>
    public bool Edge1Success { get; set; }
    /// <summary>找边1 角度偏差（度），新角度 = 模板角度 + Edge1AngleDev</summary>
    public double Edge1AngleDev { get; set; }

    /// <summary>找边2 是否成功</summary>
    public bool Edge2Success { get; set; }
    /// <summary>找边2 角度偏差（度），新角度 = 模板角度 + Edge2AngleDev</summary>
    public double Edge2AngleDev { get; set; }

    /// <summary>找点是否成功</summary>
    public bool PointSuccess { get; set; }
    /// <summary>交点 X 偏差（像素），新交点 = 模板交点 + PointDevX</summary>
    public double PointDevX { get; set; }
    /// <summary>交点 Y 偏差（像素），新交点 = 模板交点 + PointDevY</summary>
    public double PointDevY { get; set; }

    /// <summary>带绘制结果的彩色 Mat（调用方负责释放），仅 Inspect 传入了 colorImage 时非空</summary>
    public Mat? DrawMat { get; set; }
}
