using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Text.Json.Serialization;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.VisionEditor.Models;
using AFOCS.VisionEditor.Services;
using Emgu.CV;
using VisionToolkit.EdgeFinder;

namespace AFOCS.VisionEditor.Nodes
{
    /// <summary>
    /// 找边节点：用卡尺（Caliper）算法在图像中定位一条边，输出直线的起点/终点/角度/长度。
    /// </summary>
    [NodeDefinition("Vision.FindEdge", "找边", "视觉")]
    [Export(typeof(IVisionNodeDefinition))]
    public class FindEdgeNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        // ===== 可编辑配置属性 =====

        private string _imagePath = "";
        [DisplayName("图像路径")]
        public string ImagePath { get => _imagePath; set => SetProperty(ref _imagePath, value); }

        private double _edgeAngleDeg = 90;
        [DisplayName("边缘角度(°)")]
        public double EdgeAngleDeg { get => _edgeAngleDeg; set => SetProperty(ref _edgeAngleDeg, value); }

        private int _caliperCount = 20;
        [DisplayName("卡尺数量")]
        public int CaliperCount { get => _caliperCount; set => SetProperty(ref _caliperCount, value); }

        private double _caliperWidth = 5;
        [DisplayName("卡尺宽度(px)")]
        public double CaliperWidth { get => _caliperWidth; set => SetProperty(ref _caliperWidth, value); }

        private double _searchHalf = 40;
        [DisplayName("搜索半长(px)")]
        public double SearchHalf { get => _searchHalf; set => SetProperty(ref _searchHalf, value); }

        private double _inlierThreshold = 0.8;
        [DisplayName("内点阈值")]
        public double InlierThreshold { get => _inlierThreshold; set => SetProperty(ref _inlierThreshold, value); }

        // ===== 输入端口 =====

        private Mat? _image;
        [Browsable(false)]
        [NodePort("Image", "图像", NodePortType.Image, true)]
        [JsonIgnore]
        public Mat? Image { get => _image; set => SetProperty(ref _image, value); }

        // ===== 输出端口 =====

        private CaliperEdgeFinder.Result? _edge;
        [Browsable(false)]
        [NodePort("Edge", "边", NodePortType.Object, false)]
        [JsonIgnore]
        public CaliperEdgeFinder.Result? Edge { get => _edge; set => SetProperty(ref _edge, value); }

        private double _angleDeg;
        [DisplayName("角度(°)")]
        [NodePort("AngleDeg", "角度(°)", NodePortType.Double, false)]
        public double AngleDeg { get => _angleDeg; set => SetProperty(ref _angleDeg, value); }

        private double _length;
        [DisplayName("长度(px)")]
        [NodePort("Length", "长度(px)", NodePortType.Double, false)]
        public double Length { get => _length; set => SetProperty(ref _length, value); }

        private double _startX;
        [DisplayName("起点X")]
        [NodePort("StartX", "起点X", NodePortType.Double, false)]
        public double StartX { get => _startX; set => SetProperty(ref _startX, value); }

        private double _startY;
        [DisplayName("起点Y")]
        [NodePort("StartY", "起点Y", NodePortType.Double, false)]
        public double StartY { get => _startY; set => SetProperty(ref _startY, value); }

        private double _endX;
        [DisplayName("终点X")]
        [NodePort("EndX", "终点X", NodePortType.Double, false)]
        public double EndX { get => _endX; set => SetProperty(ref _endX, value); }

        private double _endY;
        [DisplayName("终点Y")]
        [NodePort("EndY", "终点Y", NodePortType.Double, false)]
        public double EndY { get => _endY; set => SetProperty(ref _endY, value); }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            using var grayImage = VisionImageHelper.LoadGray(Image, ImagePath);

            var result = CaliperEdgeFinder.Detect(
                grayImage,
                EdgeAngleDeg,
                Math.Max(2, CaliperCount),
                CaliperWidth,
                SearchHalf,
                InlierThreshold);

            if (result.TotalSamples < 2)
                throw new InvalidOperationException("找边失败：未找到有效的边缘点");

            Edge = result;
            AngleDeg = result.AngleDeg;
            Length = result.Length;
            StartX = result.Start.X;
            StartY = result.Start.Y;
            EndX = result.End.X;
            EndY = result.End.Y;

            return Task.FromResult(new Dictionary<string, object?>
            {
                ["Edge"] = Edge,
                ["AngleDeg"] = AngleDeg,
                ["Length"] = Length,
                ["StartX"] = StartX,
                ["StartY"] = StartY,
                ["EndX"] = EndX,
                ["EndY"] = EndY
            });
        }
    }
}
