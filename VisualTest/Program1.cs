﻿using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using VisionToolkit.EdgeFinder;

namespace VisualTest;

class Program
{
    static void Main(string[] args)
    {
        string imagePath = "images";
        string outputImagePath = "images";

        if (!Directory.Exists(outputImagePath))
            Directory.CreateDirectory(outputImagePath);

        // ═══════════════ LSD vs 卡尺 对比测试 ═══════════════
        string roiFile = "lsd_test1.png";
        using Mat roiColor = CvInvoke.Imread(Path.Combine(imagePath, roiFile), ImreadModes.ColorRgb);
        using Mat roiGray = new();
        CvInvoke.CvtColor(roiColor, roiGray, ColorConversion.Bgr2Gray);
        Console.WriteLine($"ROI image size: {roiGray.Rows},{roiGray.Cols}");
        Console.WriteLine();

        // ── 方法1: LSD ──
        Console.WriteLine("=== Method 1: LSD ===");
        var lsd = LsdEdgeFinder.Detect(roiGray);
        Console.WriteLine($"LSD detected {lsd.TotalDetected} line segments in ROI");
        if (lsd.Length > 0)
        {
            Console.WriteLine($"  start ({lsd.Start.X:F2}, {lsd.Start.Y:F2})");
            Console.WriteLine($"  end   ({lsd.End.X:F2}, {lsd.End.Y:F2})");
            Console.WriteLine($"  len={lsd.Length:F1}px  angle={lsd.AngleDeg:F1}°");
        }

        // ── 方法2: 卡尺 ──
        Console.WriteLine("\n=== Method 2: Caliper ===");
        // edgeAngleDeg: 0°=横边, 90°=竖边, 45°=斜边↗, 135°=斜边↘
        var caliper = CaliperEdgeFinder.Detect(roiGray, edgeAngleDeg: 90);

        Console.WriteLine($"  start ({caliper.Start.X:F4}, {caliper.Start.Y:F4})");
        Console.WriteLine($"  end   ({caliper.End.X:F4}, {caliper.End.Y:F4})");
        Console.WriteLine($"  len={caliper.Length:F1}px  angle={caliper.AngleDeg:F1}°");
        Console.WriteLine($"  inliers={caliper.InlierCount}/{caliper.TotalSamples}");

        // ── 对比图：LSD(蓝) + 卡尺(红) + 采样点(绿) ──
        using (Mat compareImg = roiColor.Clone())
        {
            // 卡尺 — 红色粗线
            CvInvoke.Line(compareImg,
                Point.Round(caliper.Start), Point.Round(caliper.End),
                new MCvScalar(0, 0, 255), 2);

            // 卡尺采样点 — 绿色圆点
            foreach (var sp in caliper.ScanPoints)
            {
                CvInvoke.Circle(compareImg, Point.Round(sp),
                    2, new MCvScalar(0, 255, 0), -1);
            }

            // LSD — 蓝色线
            if (lsd.Length > 0)
            {
                CvInvoke.Line(compareImg,
                    Point.Round(lsd.Start), Point.Round(lsd.End),
                    new MCvScalar(255, 0, 0), 1);
            }

            CvInvoke.Imwrite(Path.Combine(outputImagePath, "compare_result.png"), compareImg);
        }
        Console.WriteLine("\nSaved: compare_result.png  (red=caliper, blue=lsd, green=scan points)");
    }
}
