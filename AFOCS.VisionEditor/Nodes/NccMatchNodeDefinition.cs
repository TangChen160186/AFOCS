using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Text.Json.Serialization;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.VisionEditor.Models;
using AFOCS.VisionEditor.Services;
using Emgu.CV;
using Emgu.CV.CvEnum;
using VisionToolkit.TemplateMatcher;

namespace AFOCS.VisionEditor.Nodes
{
    /// <summary>
    /// NCC 模板匹配节点：在图像中搜索模板，输出匹配结果（中心、角度、分数）。
    /// </summary>
    [NodeDefinition("Vision.NccMatch", "NCC模板匹配", "视觉")]
    [Export(typeof(IVisionNodeDefinition))]
    public class NccMatchNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        // ===== 可编辑配置属性（右侧属性面板） =====

        private string _templatePath = "";
        [DisplayName("模板图片路径")]
        public string TemplatePath { get => _templatePath; set => SetProperty(ref _templatePath, value); }

        private string _imagePath = "";
        [DisplayName("图像路径")]
        public string ImagePath { get => _imagePath; set => SetProperty(ref _imagePath, value); }

        private double _scoreThreshold = 0.5;
        [DisplayName("分数阈值")]
        public double ScoreThreshold { get => _scoreThreshold; set => SetProperty(ref _scoreThreshold, value); }

        private double _angle = 0;
        [DisplayName("角度搜索范围(°)")]
        public double Angle { get => _angle; set => SetProperty(ref _angle, value); }

        private int _maxCount = 200;
        [DisplayName("最大匹配数")]
        public int MaxCount { get => _maxCount; set => SetProperty(ref _maxCount, value); }

        private double _iouThreshold = 0;
        [DisplayName("IoU阈值")]
        public double IouThreshold { get => _iouThreshold; set => SetProperty(ref _iouThreshold, value); }

        private double _minArea = 256;
        [DisplayName("模板最小面积")]
        public double MinArea { get => _minArea; set => SetProperty(ref _minArea, value); }

        private bool _subPixel = true;
        [DisplayName("亚像素")]
        public bool SubPixel { get => _subPixel; set => SetProperty(ref _subPixel, value); }

        // ===== 输入端口 =====

        private Mat? _image;
        [Browsable(false)]
        [NodePort("Image", "图像", NodePortType.Image, true)]
        [JsonIgnore]
        public Mat? Image { get => _image; set => SetProperty(ref _image, value); }

        // ===== 输出端口（执行后填充） =====

        private List<MatchResult>? _matches;
        [Browsable(false)]
        [NodePort("Matches", "匹配结果", NodePortType.Object, false)]
        [JsonIgnore]
        public List<MatchResult>? Matches { get => _matches; set => SetProperty(ref _matches, value); }

        private int _count;
        [DisplayName("匹配数量")]
        [NodePort("Count", "匹配数量", NodePortType.Int, false)]
        public int Count { get => _count; set => SetProperty(ref _count, value); }

        private double _centerX;
        [DisplayName("中心X")]
        [NodePort("CenterX", "中心X", NodePortType.Double, false)]
        public double CenterX { get => _centerX; set => SetProperty(ref _centerX, value); }

        private double _centerY;
        [DisplayName("中心Y")]
        [NodePort("CenterY", "中心Y", NodePortType.Double, false)]
        public double CenterY { get => _centerY; set => SetProperty(ref _centerY, value); }

        private double _matchAngle;
        [DisplayName("匹配角度(°)")]
        [NodePort("MatchAngle", "匹配角度", NodePortType.Double, false)]
        public double MatchAngle { get => _matchAngle; set => SetProperty(ref _matchAngle, value); }

        private double _score;
        [DisplayName("最佳分数")]
        [NodePort("Score", "最佳分数", NodePortType.Double, false)]
        public double Score { get => _score; set => SetProperty(ref _score, value); }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            // 模板必须存在
            if (string.IsNullOrWhiteSpace(TemplatePath) || !File.Exists(TemplatePath))
                throw new InvalidOperationException("模板图片路径无效或文件不存在");

            using var template = CvInvoke.Imread(TemplatePath, ImreadModes.Grayscale);
            if (template == null || template.IsEmpty)
                throw new InvalidOperationException($"模板图片加载失败: {TemplatePath}");

            using var grayImage = VisionImageHelper.LoadGray(Image, ImagePath);

            var param = new MatcherParam
            {
                MaxCount = Math.Max(1, MaxCount),
                ScoreThreshold = ScoreThreshold,
                IouThreshold = IouThreshold,
                Angle = Angle,
                MinArea = MinArea
            };

            using var matcher = new PatternMatcher(param) { SubPixel = SubPixel };
            if (!matcher.SetTemplate(template))
                throw new InvalidOperationException("模板学习失败（模板图像无效）");

            int resultCount = matcher.Match(grayImage, out var matchResults);
            if (resultCount < 0)
                throw new InvalidOperationException($"模板匹配失败，错误码: {resultCount}");

            Matches = matchResults;
            Count = matchResults.Count;

            var best = matchResults.FirstOrDefault();
            CenterX = best?.Center.X ?? 0;
            CenterY = best?.Center.Y ?? 0;
            MatchAngle = best?.Angle ?? 0;
            Score = best?.Score ?? 0;

            return Task.FromResult(new Dictionary<string, object?>
            {
                ["Matches"] = Matches,
                ["Count"] = Count,
                ["CenterX"] = CenterX,
                ["CenterY"] = CenterY,
                ["MatchAngle"] = MatchAngle,
                ["Score"] = Score
            });
        }
    }
}
