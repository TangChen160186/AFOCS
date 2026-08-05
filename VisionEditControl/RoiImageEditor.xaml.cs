using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VisionEditControl
{
    public class RoiChangedEventArgs : EventArgs
    {
        public Rect Roi { get; }
        public Size ImageSize { get; }
        public double RotationAngle { get; }

        public RoiChangedEventArgs(Rect roi, Size imageSize, double rotationAngle)
        {
            Roi = roi;
            ImageSize = imageSize;
            RotationAngle = rotationAngle;
        }
    }

    public partial class RoiImageEditor : UserControl
    {
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(RoiImageEditor),
                new PropertyMetadata(null, OnImageSourceChanged));

        public static readonly DependencyProperty RoiRectProperty =
            DependencyProperty.Register(nameof(RoiRect), typeof(Rect), typeof(RoiImageEditor),
                new FrameworkPropertyMetadata(Rect.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRoiChanged));

        public static readonly DependencyProperty RotationAngleProperty =
            DependencyProperty.Register(nameof(RotationAngle), typeof(double), typeof(RoiImageEditor),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRoiChanged));

        public ImageSource? ImageSource
        {
            get => (ImageSource?)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        public Rect RoiRect
        {
            get => (Rect)GetValue(RoiRectProperty);
            set => SetValue(RoiRectProperty, value);
        }

        public double RotationAngle
        {
            get => (double)GetValue(RotationAngleProperty);
            set => SetValue(RotationAngleProperty, value);
        }

        /// <summary>默认 ROI 区域（像素坐标），图片首次加载后自动应用</summary>
        public Rect DefaultRoiRect { get; set; } = Rect.Empty;

        /// <summary>默认旋转角度（度），与 DefaultRoiRect 配合使用</summary>
        public double DefaultRotationAngle { get; set; } = 0;

        public event EventHandler<RoiChangedEventArgs>? RoiChanged;

        private readonly Rectangle[] _handles;
        /// <summary>手柄相对 ROI 中心的局部坐标比例（-0.5~0.5，顺序 TL..BR）</summary>
        private static readonly (double X, double Y)[] HandleLocalFractions =
        {
            (-0.5, -0.5), ( 0.0, -0.5), ( 0.5, -0.5), // TL, TC, TR
            (-0.5,  0.0), ( 0.5,  0.0),               // ML, MR
            (-0.5,  0.5), ( 0.0,  0.5), ( 0.5,  0.5), // BL, BC, BR
        };
        private readonly RotateTransform _roiRectRotate;
        /// <summary>每个手柄的 RotateTransform，用于让手柄视觉跟随旋转</summary>
        private readonly RotateTransform[] _handleRotates;

        private enum InteractionMode { None, Creating, Moving, Resizing, Rotating }
        private InteractionMode _mode = InteractionMode.None;
        private Point _dragStart;
        private Rect _roiAtDragStart;
        private double _angleAtDragStart;
        private int _activeHandle = -1; // 0=TL..7=BR, 8=旋转手柄

        private Rect _imageRenderRect;

        public RoiImageEditor()
        {
            InitializeComponent();

            _handles = new[] { handleTL, handleTC, handleTR, handleML, handleMR, handleBL, handleBC, handleBR };

            // 给每个手柄添加旋转变换
            _handleRotates = new RotateTransform[8];
            for (int i = 0; i < 8; i++)
            {
                _handleRotates[i] = new RotateTransform(0);
                _handles[i].RenderTransform = _handleRotates[i];
                _handles[i].RenderTransformOrigin = new Point(0.5, 0.5);
            }

            // ROI 矩形的旋转变换
            _roiRectRotate = new RotateTransform(0);
            roiRect.RenderTransform = _roiRectRotate;

            imgMain.SizeChanged += (_, _) => UpdateOverlay();
            imgMain.LayoutUpdated += (_, _) => UpdateOverlay();
            Loaded += (_, _) => imgMain.Source = ImageSource;
        }

        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (RoiImageEditor)d;
            editor.imgMain.Source = (ImageSource)e.NewValue;
            editor.UpdateOverlay();
            editor.TryApplyDefaultRoi();
        }

        private void TryApplyDefaultRoi()
        {
            if (imgMain.Source is not BitmapSource bs) return;
            if (RoiRect != Rect.Empty && RoiRect.Width > 0) return; // 已有 ROI 则不覆盖

            Rect roi;
            if (DefaultRoiRect != Rect.Empty && DefaultRoiRect.Width > 0 && DefaultRoiRect.Height > 0)
            {
                roi = DefaultRoiRect;
            }
            else
            {
                // 默认创建覆盖整张图的 ROI
                roi = new Rect(0, 0, bs.PixelWidth, bs.PixelHeight);
            }

            SetRoi(roi, DefaultRotationAngle);
        }

        private static void OnRoiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RoiImageEditor)d).UpdateOverlay();
        }

        #region 坐标转换

        private Rect GetImageRenderRect()
        {
            double ctrlW = overlayCanvas.ActualWidth, ctrlH = overlayCanvas.ActualHeight;

            if (imgMain.Source is not BitmapSource bs || bs.PixelWidth <= 0 || bs.PixelHeight <= 0)
                return new Rect(0, 0, ctrlW, ctrlH);

            double imgW = bs.PixelWidth, imgH = bs.PixelHeight;
            if (ctrlW <= 0 || ctrlH <= 0) return new Rect(0, 0, 0, 0);

            double scale = Math.Min(ctrlW / imgW, ctrlH / imgH);
            double rw = imgW * scale, rh = imgH * scale;
            double ox = (ctrlW - rw) / 2, oy = (ctrlH - rh) / 2;
            return new Rect(ox, oy, rw, rh);
        }

        private Point CanvasToPixel(Point canvasPt)
        {
            var r = GetImageRenderRect();
            if (r.Width <= 0 || r.Height <= 0) return new Point(0, 0);
            var bs = (BitmapSource)imgMain.Source;
            double x = (canvasPt.X - r.X) / r.Width * bs.PixelWidth;
            double y = (canvasPt.Y - r.Y) / r.Height * bs.PixelHeight;
            return new Point(Clamp(x, 0, bs.PixelWidth), Clamp(y, 0, bs.PixelHeight));
        }

        private Rect PixelToCanvasRect(Rect pixelRect)
        {
            var r = GetImageRenderRect();
            if (r.Width <= 0 || r.Height <= 0) return Rect.Empty;
            var bs = (BitmapSource)imgMain.Source;
            double sx = r.Width / bs.PixelWidth, sy = r.Height / bs.PixelHeight;
            return new Rect(r.X + pixelRect.X * sx, r.Y + pixelRect.Y * sy,
                            pixelRect.Width * sx, pixelRect.Height * sy);
        }

        /// <summary>ROI 局部偏移变换到画布空间（正旋转）</summary>
        private (double x, double y) TransformToCanvas(double lx, double ly)
        {
            double a = RotationAngle * Math.PI / 180;
            double cos = Math.Cos(a), sin = Math.Sin(a);
            return (lx * cos - ly * sin, lx * sin + ly * cos);
        }

        #endregion

        #region 覆盖层更新

        private void UpdateOverlay()
        {
            if (imgMain.Source is not BitmapSource bs || bs.PixelWidth <= 0) return;
            _imageRenderRect = GetImageRenderRect();

            var roiPixel = RoiRect;
            if (roiPixel == Rect.Empty || roiPixel.Width <= 0 || roiPixel.Height <= 0)
            {
                HideAllRoiElements();
                UpdateMask(Rect.Empty, 0, new Point(0, 0));
                return;
            }

            var roiCanvas = PixelToCanvasRect(roiPixel);
            double angle = RotationAngle;
            Point center = new Point(roiCanvas.X + roiCanvas.Width / 2, roiCanvas.Y + roiCanvas.Height / 2);

            // ROI 矩形
            Canvas.SetLeft(roiRect, roiCanvas.X);
            Canvas.SetTop(roiRect, roiCanvas.Y);
            roiRect.Width = roiCanvas.Width;
            roiRect.Height = roiCanvas.Height;
            _roiRectRotate.Angle = angle;
            _roiRectRotate.CenterX = roiCanvas.Width / 2;
            _roiRectRotate.CenterY = roiCanvas.Height / 2;
            roiRect.Visibility = Visibility.Visible;

            // 手柄
            UpdateHandles(roiCanvas, center, angle);

            // 旋转手柄
            UpdateRotationHandle(center, angle);

            // 遮罩
            UpdateMask(roiCanvas, angle, center);

            // 信息标签
            UpdateRoiInfo(roiPixel, angle, roiCanvas);
        }

        private void HideAllRoiElements()
        {
            roiRect.Visibility = Visibility.Collapsed;
            roiInfoBorder.Visibility = Visibility.Collapsed;
            rotateHandle.Visibility = Visibility.Collapsed;
            rotateLine.Visibility = Visibility.Collapsed;
            foreach (var h in _handles) h.Visibility = Visibility.Collapsed;
        }

        private void UpdateHandles(Rect r, Point center, double angle)
        {
            double hw = 4;
            // 局部坐标下，各手柄相对于 ROI 中心的偏移
            (double dx, double dy)[] localOffsets =
            {
                (-r.Width/2 - hw, -r.Height/2 - hw),   // TL
                (0, -r.Height/2 - hw),                   // TC
                (r.Width/2 - hw, -r.Height/2 - hw),     // TR
                (-r.Width/2 - hw, 0),                    // ML
                (r.Width/2 - hw, 0),                     // MR
                (-r.Width/2 - hw, r.Height/2 - hw),     // BL
                (0, r.Height/2 - hw),                    // BC
                (r.Width/2 - hw, r.Height/2 - hw),      // BR
            };

            for (int i = 0; i < 8; i++)
            {
                var rotated = TransformToCanvas(localOffsets[i].dx, localOffsets[i].dy);
                Canvas.SetLeft(_handles[i], center.X + rotated.x);
                Canvas.SetTop(_handles[i], center.Y + rotated.y);
                _handleRotates[i].Angle = angle;
                _handles[i].Visibility = Visibility.Visible;
            }
        }

        private void UpdateRotationHandle(Point roiCenter, double angle)
        {
            rotateHandle.Visibility = Visibility.Visible;
            rotateLine.Visibility = Visibility.Visible;

            // 旋转手柄在 ROI 上方（局部坐标: 中心正上方 30px）
            double offsetY = -30;
            var rotated = TransformToCanvas(0, offsetY);
            double rhX = roiCenter.X + rotated.x;
            double rhY = roiCenter.Y + rotated.y;

            Canvas.SetLeft(rotateHandle, rhX - 7);
            Canvas.SetTop(rotateHandle, rhY - 7);

            // 连接线：从顶部中心到旋转手柄
            var topCenter = TransformToCanvas(0, -(PixelToCanvasRect(RoiRect).Height / 2));
            rotateLine.X1 = roiCenter.X + topCenter.x;
            rotateLine.Y1 = roiCenter.Y + topCenter.y;
            rotateLine.X2 = rhX;
            rotateLine.Y2 = rhY;
        }

        private void UpdateMask(Rect roiCanvas, double angle, Point center)
        {
            double w = overlayCanvas.ActualWidth;
            double h = overlayCanvas.ActualHeight;
            fullRectGeo.Rect = new Rect(0, 0, Math.Max(w, 1), Math.Max(h, 1));

            if (roiCanvas == Rect.Empty || roiCanvas.Width <= 0)
            {
                roiHoleGeo.Rect = new Rect(0, 0, 0, 0);
                roiHoleGeo.Transform = Transform.Identity;
                return;
            }

            roiHoleGeo.Rect = new Rect(roiCanvas.X, roiCanvas.Y, roiCanvas.Width, roiCanvas.Height);
            roiHoleGeo.Transform = new RotateTransform(angle, center.X, center.Y);
        }

        private void UpdateRoiInfo(Rect roiPixel, double angle, Rect roiCanvas)
        {
            txtRoiInfo.Text = $"({roiPixel.X:F0},{roiPixel.Y:F0}) {roiPixel.Width:F0}×{roiPixel.Height:F0}  {angle:F1}°";
            roiInfoBorder.Visibility = Visibility.Visible;

            // 放在 ROI 左上角（不跟随旋转，保持可读）
            double x = roiCanvas.Left + 4;
            double y = roiCanvas.Top + 4;
            Canvas.SetLeft(roiInfoBorder, Clamp(x, 0, overlayCanvas.ActualWidth - 150));
            Canvas.SetTop(roiInfoBorder, Clamp(y, 0, overlayCanvas.ActualHeight - 26));
        }

        #endregion

        #region 鼠标交互

        // 角度吸附阈值
        private const double SnapThreshold = 5.0;

        private void OverlayCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (imgMain.Source is not BitmapSource) return;
            overlayCanvas.CaptureMouse();

            var pos = e.GetPosition(overlayCanvas);
            _dragStart = pos;
            _roiAtDragStart = RoiRect;
            _angleAtDragStart = RotationAngle;

            // 1. 检查旋转手柄
            if (RoiRect != Rect.Empty && HitTestRotateHandle(pos))
            {
                _mode = InteractionMode.Rotating;
                return;
            }

            // 2. 检查缩放手柄
            int hi = HitTestHandle(pos);
            if (hi >= 0 && RoiRect != Rect.Empty)
            {
                _mode = InteractionMode.Resizing;
                _activeHandle = hi;
                return;
            }

            // 3. 检查是否在旋转后的 ROI 内部
            if (RoiRect != Rect.Empty && HitTestRoiInterior(pos))
            {
                _mode = InteractionMode.Moving;
                return;
            }

            // 4. 创建新 ROI（先重置旋转角度）
            _angleAtDragStart = 0;
            RotationAngle = 0;
            _mode = InteractionMode.Creating;
        }

        private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(overlayCanvas);
            UpdateCoordDisplay(pos);

            if (imgMain.Source is not BitmapSource) return;

            switch (_mode)
            {
                case InteractionMode.Creating:  UpdateCreating(pos); break;
                case InteractionMode.Moving:    UpdateMoving(pos); break;
                case InteractionMode.Resizing:  UpdateResizing(pos); break;
                case InteractionMode.Rotating:  UpdateRotating(pos); break;
                case InteractionMode.None:      UpdateCursor(pos); break;
            }
        }

        private void OverlayCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            overlayCanvas.ReleaseMouseCapture();

            if (_mode == InteractionMode.Creating)
            {
                var roi = RoiRect;
                if (roi.Width < 5 || roi.Height < 5)
                {
                    RoiRect = Rect.Empty;
                    RotationAngle = 0;
                }
                FireRoiChanged();
            }

            _mode = InteractionMode.None;
            _activeHandle = -1;
        }

        private void OverlayCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            coordBorder.Visibility = Visibility.Collapsed;
        }

        private void UpdateCoordDisplay(Point canvasPos)
        {
            if (imgMain.Source is not BitmapSource)
            {
                coordBorder.Visibility = Visibility.Collapsed;
                return;
            }
            var pixel = CanvasToPixel(canvasPos);
            txtCoord.Text = $"({pixel.X:F0}, {pixel.Y:F0})";
            double x = canvasPos.X + 16, y = canvasPos.Y + 16;
            if (x + 100 > overlayCanvas.ActualWidth) x = canvasPos.X - 100;
            if (y + 30 > overlayCanvas.ActualHeight) y = canvasPos.Y - 30;
            Canvas.SetLeft(coordBorder, x);
            Canvas.SetTop(coordBorder, y);
            coordBorder.Visibility = Visibility.Visible;
        }

        private void UpdateCreating(Point pos)
        {
            var startPixel = CanvasToPixel(_dragStart);
            var endPixel = CanvasToPixel(pos);
            var bs = (BitmapSource)imgMain.Source;

            double x = Math.Max(0, Math.Min(startPixel.X, endPixel.X));
            double y = Math.Max(0, Math.Min(startPixel.Y, endPixel.Y));
            double w = Math.Min(Math.Abs(endPixel.X - startPixel.X), bs.PixelWidth - x);
            double h = Math.Min(Math.Abs(endPixel.Y - startPixel.Y), bs.PixelHeight - y);

            RoiRect = new Rect(x, y, w, h);
            FireRoiChanged();
        }

        private void UpdateMoving(Point pos)
        {
            var sp = CanvasToPixel(_dragStart);
            var cp = CanvasToPixel(pos);
            double dx = cp.X - sp.X, dy = cp.Y - sp.Y;
            var bs = (BitmapSource)imgMain.Source;
            double nx = Clamp(_roiAtDragStart.X + dx, 0, bs.PixelWidth - _roiAtDragStart.Width);
            double ny = Clamp(_roiAtDragStart.Y + dy, 0, bs.PixelHeight - _roiAtDragStart.Height);

            RoiRect = new Rect(nx, ny, _roiAtDragStart.Width, _roiAtDragStart.Height);
            FireRoiChanged();
        }

        private void UpdateResizing(Point pos)
        {
            var bs = (BitmapSource)imgMain.Source;
            if (bs == null) return;

            double angleRad = RotationAngle * Math.PI / 180;
            double cos = Math.Cos(angleRad), sin = Math.Sin(angleRad);
            // ROI 局部坐标轴在画布上的方向（u: 宽度方向, v: 高度方向）
            double ux = cos, uy = sin;
            double vx = -sin, vy = cos;

            var start = _roiAtDragStart;
            double w0 = start.Width, h0 = start.Height;
            double cx0 = start.X + w0 / 2, cy0 = start.Y + h0 / 2;

            // 手柄局部比例，以及其"对侧"锚点比例（缩放时锚点保持不动）
            var handle = HandleLocalFractions[_activeHandle];
            double afx = -handle.X, afy = -handle.Y;

            // 锚点的像素坐标（拖拽开始时固定）
            double ax = cx0 + afx * w0 * ux + afy * h0 * vx;
            double ay = cy0 + afx * w0 * uy + afy * h0 * vy;

            // 鼠标相对锚点，沿 ROI 局部轴的分量
            var mp = CanvasToPixel(pos);
            double lu = (mp.X - ax) * ux + (mp.Y - ay) * uy; // 沿宽度方向
            double lv = (mp.X - ax) * vx + (mp.Y - ay) * vy; // 沿高度方向

            const double minSize = 5;
            double w1, h1;
            if (_activeHandle is 0 or 2 or 5 or 7)    // 角手柄：宽高同时变化
            {
                w1 = Math.Max(Math.Abs(lu), minSize);
                h1 = Math.Max(Math.Abs(lv), minSize);
            }
            else if (_activeHandle is 1 or 6)         // 上/下边手柄：只变高度
            {
                w1 = w0;
                h1 = Math.Max(Math.Abs(lv), minSize);
            }
            else                                      // 左/右边手柄：只变宽度
            {
                w1 = Math.Max(Math.Abs(lu), minSize);
                h1 = h0;
            }

            w1 = Math.Min(w1, bs.PixelWidth);
            h1 = Math.Min(h1, bs.PixelHeight);

            // 新中心：使锚点保持在原位置（C' = 锚点 - R·锚点局部坐标）
            double ncx = ax - afx * w1 * ux - afy * h1 * vx;
            double ncy = ay - afx * w1 * uy - afy * h1 * vy;

            double left = Clamp(ncx - w1 / 2, 0, bs.PixelWidth - w1);
            double top = Clamp(ncy - h1 / 2, 0, bs.PixelHeight - h1);

            RoiRect = new Rect(left, top, w1, h1);
            FireRoiChanged();
        }

        private void UpdateRotating(Point pos)
        {
            var roiCanvas = PixelToCanvasRect(RoiRect);
            Point center = new Point(roiCanvas.X + roiCanvas.Width / 2, roiCanvas.Y + roiCanvas.Height / 2);

            double angle = Math.Atan2(pos.Y - center.Y, pos.X - center.X) * 180 / Math.PI + 90;
            angle = ((angle % 360) + 360) % 360;

            // 角度吸附（靠近常见角度时吸附）
            double snapped = SnapAngle(angle);
            RotationAngle = snapped;
            FireRoiChanged();
        }

        private void UpdateCursor(Point pos)
        {
            if (RoiRect == Rect.Empty)
            {
                overlayCanvas.Cursor = Cursors.Cross;
                return;
            }

            // 旋转手柄
            if (HitTestRotateHandle(pos)) { overlayCanvas.Cursor = Cursors.Hand; return; }

            // 缩放手柄
            int hi = HitTestHandle(pos);
            if (hi >= 0) { overlayCanvas.Cursor = _handles[hi].Cursor; return; }

            // ROI 内部（旋转后的包围盒近似检测）
            if (HitTestRoiInterior(pos)) { overlayCanvas.Cursor = Cursors.SizeAll; return; }

            overlayCanvas.Cursor = Cursors.Cross;
        }

        #endregion

        #region 命中检测

        private bool HitTestRotateHandle(Point pos)
        {
            if (rotateHandle.Visibility != Visibility.Visible) return false;
            double cx = Canvas.GetLeft(rotateHandle) + 7;
            double cy = Canvas.GetTop(rotateHandle) + 7;
            return Math.Abs(pos.X - cx) < 10 && Math.Abs(pos.Y - cy) < 10;
        }

        private int HitTestHandle(Point pos)
        {
            double threshold = 7;
            for (int i = 0; i < 8; i++)
            {
                if (_handles[i].Visibility != Visibility.Visible) continue;
                double cx = Canvas.GetLeft(_handles[i]) + 4;
                double cy = Canvas.GetTop(_handles[i]) + 4;
                if (Math.Abs(pos.X - cx) <= threshold && Math.Abs(pos.Y - cy) <= threshold)
                    return i;
            }
            return -1;
        }

        /// <summary>检测点是否在旋转后的 ROI 内部</summary>
        private bool HitTestRoiInterior(Point pos)
        {
            var roiCanvas = PixelToCanvasRect(RoiRect);
            Point center = new Point(roiCanvas.X + roiCanvas.Width / 2, roiCanvas.Y + roiCanvas.Height / 2);

            // 将鼠标位置变换到 ROI 局部坐标
            double a = -RotationAngle * Math.PI / 180;
            double cos = Math.Cos(a), sin = Math.Sin(a);
            double lx = (pos.X - center.X) * cos - (pos.Y - center.Y) * sin;
            double ly = (pos.X - center.X) * sin + (pos.Y - center.Y) * cos;

            return Math.Abs(lx) <= roiCanvas.Width / 2 && Math.Abs(ly) <= roiCanvas.Height / 2;
        }

        #endregion

        #region 公共方法

        public void ClearRoi()
        {
            RoiRect = Rect.Empty;
            RotationAngle = 0;
            FireRoiChanged();
        }

        public void SetRoi(Rect pixelRect, double rotationAngle = 0)
        {
            var bs = imgMain.Source as BitmapSource;
            if (bs != null)
            {
                pixelRect.X = Clamp(pixelRect.X, 0, bs.PixelWidth);
                pixelRect.Y = Clamp(pixelRect.Y, 0, bs.PixelHeight);
                pixelRect.Width = Clamp(pixelRect.Width, 0, bs.PixelWidth - pixelRect.X);
                pixelRect.Height = Clamp(pixelRect.Height, 0, bs.PixelHeight - pixelRect.Y);
            }
            RoiRect = pixelRect;
            RotationAngle = rotationAngle;
            FireRoiChanged();
        }

        #endregion

        private void FireRoiChanged()
        {
            var bs = imgMain.Source as BitmapSource;
            var imgSize = bs != null ? new Size(bs.PixelWidth, bs.PixelHeight) : Size.Empty;
            RoiChanged?.Invoke(this, new RoiChangedEventArgs(RoiRect, imgSize, RotationAngle));
        }

        private static double SnapAngle(double angle)
        {
            double[] snaps = { 0, 45, 90, 135, 180, 225, 270, 315, 360 };
            foreach (var s in snaps)
            {
                double diff = Math.Abs(angle - s);
                if (diff < SnapThreshold) return s % 360;
                // 也检查跨越 0° 的情况
                if (diff > 360 - SnapThreshold) return 0;
            }
            return angle;
        }

        private static double Clamp(double value, double min, double max)
            => Math.Max(min, Math.Min(max, value));
    }
}
