using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using EmguCVMatching;
using System.Drawing;
using VisualTest;

class Program
{
    // 显示用的最大窗口尺寸
    private static readonly Size MaxDisplaySize = new Size(1600, 900);

    /// <summary>将图像缩放到适合屏幕显示的大小</summary>
    private static Mat ResizeToFit(Mat image, Size maxSize)
    {
        if (image.Width <= maxSize.Width && image.Height <= maxSize.Height)
            return image.Clone();

        double scale = Math.Min(
            (double)maxSize.Width / image.Width,
            (double)maxSize.Height / image.Height);
        int newW = (int)(image.Width * scale);
        int newH = (int)(image.Height * scale);

        Mat resized = new Mat();
        CvInvoke.Resize(image, resized, new Size(newW, newH), 0, 0, Inter.Linear);
        return resized;
    }

    static void Main(string[] args)
    {
        // ======= 参数配置 =======
        var param = new MatcherParam
        {
            MatcherType = MatcherType.Pattern,
            MaxCount = 1,
            ScoreThreshold = 0.95,
            IouThreshold = 0.0,
            Angle = 90,             // ±10° 搜索范围
            MinArea = 256
        };

        // ======= 加载图片 =======
        string templatePath = "images/model1.png";
        string scenePath = "images/scene.jpg";

        if (args.Length >= 2)
        {
            templatePath = args[0];
            scenePath = args[1];
        }

        if (!File.Exists(templatePath))
        {
            Console.WriteLine($"Template image not found: {templatePath}");
            Console.WriteLine("Usage: EmguCVMatching.exe <template.png> <scene.png>");
            Console.WriteLine("Or place 'template.png' and 'scene.png' in the same directory.");
            return;
        }

        if (!File.Exists(scenePath))
        {
            Console.WriteLine($"Scene image not found: {scenePath}");
            return;
        }

        Console.WriteLine($"Template: {templatePath}");
        Console.WriteLine($"Scene:    {scenePath}");

        using (Mat templateImg = CvInvoke.Imread(templatePath, ImreadModes.Grayscale))
        using (Mat sceneImg = CvInvoke.Imread(scenePath, ImreadModes.Grayscale))
        {
            if (templateImg.IsEmpty) { Console.WriteLine("Failed to load template."); return; }
            if (sceneImg.IsEmpty) { Console.WriteLine("Failed to load scene."); return; }

            Console.WriteLine($"Template size: {templateImg.Width}x{templateImg.Height}");
            Console.WriteLine($"Scene size:    {sceneImg.Width}x{sceneImg.Height}");

            // ======= 创建匹配器 =======
            using (var matcher = new PatternMatcher(param))
            {
                if (!matcher.IsInited)
                {
                    Console.WriteLine("Failed to init matcher.");
                    return;
                }

                matcher.SetTemplate(templateImg);
                Console.WriteLine("Template learned.");

                // ======= 执行匹配 =======
                var sw = System.Diagnostics.Stopwatch.StartNew();
                matcher.Match(sceneImg, out var matchResults);
                sw.Stop();

                Console.WriteLine($"Match: {sw.Elapsed.TotalMilliseconds:F1}ms, found {matchResults.Count} result(s)");

                // ======= 绘制 + 显示结果 =======
                using (Mat drawFrame = CvInvoke.Imread(scenePath, ImreadModes.ColorRgb))
                {
                    foreach (var r in matchResults)
                    {
                        Console.WriteLine($"  Score={r.Score:F4}  Angle={r.Angle:F1}°  Center=({r.Center.X:F1},{r.Center.Y:F1})");

                        var pts = new Point[]
                        {
                            new Point((int)Math.Round(r.LeftTop.X), (int)Math.Round(r.LeftTop.Y)),
                            new Point((int)Math.Round(r.RightTop.X), (int)Math.Round(r.RightTop.Y)),
                            new Point((int)Math.Round(r.RightBottom.X), (int)Math.Round(r.RightBottom.Y)),
                            new Point((int)Math.Round(r.LeftBottom.X), (int)Math.Round(r.LeftBottom.Y)),
                        };

                        using (var ptsVec = new VectorOfPoint(pts))
                            CvInvoke.Polylines(drawFrame, ptsVec, true, new MCvScalar(0, 255, 0), 2);

                        CvInvoke.PutText(drawFrame, $"S:{r.Score:F2} A:{r.Angle:F1}",
                            new Point((int)r.LeftTop.X, Math.Max(0, (int)r.LeftTop.Y - 5)),
                            FontFace.HersheyComplex, 10.0, new MCvScalar(255, 0, 0), 1);
                    }

                    CvInvoke.Imwrite("result.png", drawFrame);
                    Console.WriteLine("Result saved to result.png");

                    // 缩放到适合屏幕显示
                    using (Mat displayImg = ResizeToFit(drawFrame, MaxDisplaySize))
                    {
                        CvInvoke.Imshow("Result", displayImg);
                        Console.WriteLine("Press any key to exit...");
                        CvInvoke.WaitKey();
                    }
                }
            }
        }

        CvInvoke.DestroyAllWindows();
    }
}
