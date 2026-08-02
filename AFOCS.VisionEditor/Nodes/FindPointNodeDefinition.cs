using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Drawing;
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
    /// 找点节点：由两条找到的边（直线）求出交点。
    /// 边可通过输入端口连接（来自两个找边节点），
    /// 未连接时用"边1/边2角度"参数在图像上自动找两条边再求交点。
    /// </summary>
    [NodeDefinition("Vision.FindPoint", "找点", "视觉")]
    [Export(typeof(IVisionNodeDefinition))]
    public class FindPointNodeDefinition : NodeDefinitionBase, IExecutableNode
    {
        // ===== 可编辑配置属性 =====

        private string _imagePath = "";
        [DisplayName("图像路径")]
        public string ImagePath { get => _imagePath; set => SetProperty(ref _imagePath, value); }

        private double _edge1Angle = 0;
        [DisplayName("边1角度(°)")]
        public double Edge1Angle { get => _edge1Angle; set => SetProperty(ref _edge1Angle, value); }

        private double _edge2Angle = 90;
        [DisplayName("边2角度(°)")]
        public double Edge2Angle { get => _edge2Angle; set => SetProperty(ref _edge2Angle, value); }

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

        private object? _edge1;
        [Browsable(false)]
        [NodePort("Edge1", "边1", NodePortType.Object, true)]
        [JsonIgnore]
        public object? Edge1 { get => _edge1; set => SetProperty(ref _edge1, value); }

        private object? _edge2;
        [Browsable(false)]
        [NodePort("Edge2", "边2", NodePortType.Object, true)]
        [JsonIgnore]
        public object? Edge2 { get => _edge2; set => SetProperty(ref _edge2, value); }

        // ===== 输出端口 =====

        private object? _point;
        [Browsable(false)]
        [NodePort("Point", "交点", NodePortType.Object, false)]
        [JsonIgnore]
        public object? Point { get => _point; set => SetProperty(ref _point, value); }

        private double _pointX;
        [DisplayName("交点X")]
        [NodePort("PointX", "交点X", NodePortType.Double, false)]
        public double PointX { get => _pointX; set => SetProperty(ref _pointX, value); }

        private double _pointY;
        [DisplayName("交点Y")]
        [NodePort("PointY", "交点Y", NodePortType.Double, false)]
        public double PointY { get => _pointY; set => SetProperty(ref _pointY, value); }

        public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
        {
            var e1 = Edge1 as CaliperEdgeFinder.Result;
            var e2 = Edge2 as CaliperEdgeFinder.Result;

            if (e1 == null || e2 == null)
            {
                // 两条边未连接时，用各自角度参数在图像上找两条边
                using var grayImage = VisionImageHelper.LoadGray(Image, ImagePath);
                e1 = CaliperEdgeFinder.Detect(grayImage, Edge1Angle,
                    Math.Max(2, CaliperCount), CaliperWidth, SearchHalf, InlierThreshold);
                e2 = CaliperEdgeFinder.Detect(grayImage, Edge2Angle,
                    Math.Max(2, CaliperCount), CaliperWidth, SearchHalf, InlierThreshold);

                if (e1.TotalSamples < 2)
                    throw new InvalidOperationException("找点失败：边1未找到有效的边缘点");
                if (e2.TotalSamples < 2)
                    throw new InvalidOperationException("找点失败：边2未找到有效的边缘点");
            }

            var (px, py) = IntersectLine(e1.Start, e1.End, e2.Start, e2.End);
            if (double.IsNaN(px))
                throw new InvalidOperationException("找点失败：两条边平行，无法求交点");

            PointX = px;
            PointY = py;
            Point = new PointF((float)px, (float)py);

            return Task.FromResult(new Dictionary<string, object?>
            {
                ["Point"] = Point,
                ["PointX"] = PointX,
                ["PointY"] = PointY
            });
        }

        /// <summary>计算两条线段所在直线的交点（以点斜式求解）</summary>
        private static (double X, double Y) IntersectLine(
            PointF p1, PointF p2, PointF p3, PointF p4)
        {
            double d1x = p2.X - p1.X, d1y = p2.Y - p1.Y;
            double d2x = p4.X - p3.X, d2y = p4.Y - p3.Y;

            double denom = d1x * d2y - d1y * d2x;
            if (Math.Abs(denom) < 1e-9)
                return (double.NaN, double.NaN);

            double t = ((p3.X - p1.X) * d2y - (p3.Y - p1.Y) * d2x) / denom;
            return (p1.X + t * d1x, p1.Y + t * d1y);
        }
    }
}
