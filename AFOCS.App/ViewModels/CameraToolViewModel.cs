using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using AFOCS.App.Services;
using AFOCS.Devices.Camera;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using AFOCS.Infrastructure.Extensions;
using AFOCS.VisionEditor.Services;
using Caliburn.Micro;
using HalconDotNet;
using Microsoft.Win32;
using Serilog;

namespace AFOCS.App.ViewModels;

public interface ILeftUpCameraTool : ITool;

public interface ILeftDownCameraTool : ITool;

public interface IRightUpCameraTool : ITool;

public interface IRightDownCameraTool : ITool;

/// <summary>
/// 相机实时监控面板基类：通过 HSmartWindowControlWPF 实时显示相机图像（节流轮询最新帧），
/// 订阅 <see cref="VisionInspectionMessage"/>，视觉流程执行后在对应相机图像上叠加绘制检测结果。
/// </summary>
public abstract class CameraToolViewModelBase : Tool, IHandle<VisionInspectionMessage>
{
    private ICamera _camera;
    private string _cameraName;
    private readonly ILogger _logger;
    private readonly IToastService _toastService;
    private readonly DispatcherTimer _renderTimer;

    // ---- Halcon 窗口（由 View 挂接） ----

    private HSmartWindowControlWPF? _halconControl;
    private HWindow? _halconWindow;
    private HImage? _displayImage;
    private int _displayedW = -1, _displayedH = -1;
    private bool _rendering;

    // ---- 帧缓存（SDK 回调线程写入，UI 渲染线程读取） ----

    private readonly object _frameLock = new();
    private byte[]? _latestFrameData;
    private int _latestW, _latestH;
    private bool _latestIsMono;
    private bool _hasNewFrame;
    private DateTime _lastFrameTime = DateTime.MinValue;

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set => Set(ref _isConnected, value);
    }

    /// <summary>是否允许右键保存图像（多相机查看工具等可关闭）</summary>
    public virtual bool CanSaveImage => true;

    public string LastResultText
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    protected CameraToolViewModelBase(ICamera camera, string cameraName, ILogger logger, IEventAggregator events, IToastService toastService)
    {
        _camera = camera;
        _cameraName = cameraName;
        _logger = logger;
        _toastService = toastService;
        DisplayName = $"{cameraName}实时图像";
        IsConnected = camera.IsConnected;

        events.SubscribeOnUIThread(this);

        // 订阅相机事件：SDK 回调线程缓存最新帧，UI 定时器按节流取帧渲染
        _camera.ImageReceived += OnImageReceived;

        // 节流刷新（约 15fps），避免高帧率相机回调逐帧 DispObj 造成卡顿
        _renderTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(66),
        };
        _renderTimer.Tick += (_, _) => RenderFrame();
        _renderTimer.Start();
    }

    /// <summary>View Loaded 时挂接 Halcon 窗口（渲染开关由 Screen 激活状态控制）</summary>
    public void SetHalconControl(HSmartWindowControlWPF control)
    {
        _halconControl = control;
        _halconWindow = control.HalconWindow;
    }

    /// <summary>View Unloaded 时解除窗口引用（Tool 关闭后不再刷新）</summary>
    public void ClearHalconControl()
    {
        _halconWindow = null;
        _halconControl = null;
    }

    /// <summary>切换要显示的相机（多相机查看工具使用）：改订阅事件源并清空旧帧缓存</summary>
    protected void SwitchCamera(ICamera camera, string cameraName)
    {
        _camera.ImageReceived -= OnImageReceived;
        _camera = camera;
        _cameraName = cameraName;
        _camera.ImageReceived += OnImageReceived;

        lock (_frameLock)
        {
            _latestFrameData = null;
            _hasNewFrame = false;
            _lastFrameTime = DateTime.MinValue;
        }
        _displayedW = -1;
        _displayedH = -1;

        IsConnected = camera.IsConnected;
        DisplayName = $"{cameraName}实时图像";
    }

    // ==================== 实时图像 ====================

    /// <summary>
    /// SDK 回调线程触发：拷贝最新帧到缓存并标记有更新。仅缓存，不做 UI 操作。
    /// </summary>
    private void OnImageReceived(object? sender, ImagePreviewedEventArgs e)
    {
        try
        {
            int len = e.Width * e.Height * (e.IsMono ? 1 : 3);
            lock (_frameLock)
            {
                if (_latestFrameData == null || _latestFrameData.Length < len)
                    _latestFrameData = new byte[len];
                Marshal.Copy(e.ImageData, _latestFrameData, 0, len);
                _latestW = e.Width;
                _latestH = e.Height;
                _latestIsMono = e.IsMono;
                _lastFrameTime = DateTime.UtcNow;
                _hasNewFrame = true;
            }
            IsConnected = true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[{Camera}] 缓存相机帧失败", _cameraName);
        }
    }

    /// <summary>UI 定时器节流渲染：有新帧才取帧显示，断流超过 2s 标记未连接</summary>
    private void RenderFrame()
    {
        if (_halconWindow == null || _rendering)
            return;

        byte[] data;
        int w, h;
        bool isMono;
        lock (_frameLock)
        {
            if (!_hasNewFrame)
            {
                if (DateTime.UtcNow - _lastFrameTime > TimeSpan.FromSeconds(2))
                    IsConnected = false;
                return;
            }
            if (_latestFrameData == null)
                return;

            data = new byte[_latestFrameData.Length];
            Array.Copy(_latestFrameData, data, _latestFrameData.Length);
            w = _latestW;
            h = _latestH;
            isMono = _latestIsMono;
            _hasNewFrame = false;
        }

        HImage image;
        try
        {
            image = BuildHImage(data, w, h, isMono);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[{Camera}] 构建 HImage 失败", _cameraName);
            return;
        }

        var old = _displayImage;
        _displayImage = image;
        old?.Dispose();

        _rendering = true;
        try
        {
            _halconWindow.DispObj(image);
            if (w != _displayedW || h != _displayedH)
            {
                _displayedW = w;
                _displayedH = h;
                _halconControl?.SetFullImagePart();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[{Camera}] 显示图像失败", _cameraName);
        }
        finally
        {
            _rendering = false;
        }
    }

    /// <summary>右键保存当前帧为 BMP：未连接时提示并中止，否则弹出保存对话框选择目录</summary>
    public async void SaveAsBmp()
    {
        if (!_camera.IsConnected)
        {
            _toastService.ShowWarning($"{_cameraName} 未连接，无法保存图像");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "保存图像为 BMP",
            Filter = "BMP 图片 (*.bmp)|*.bmp",
            DefaultExt = ".bmp",
            FileName = $"{_cameraName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp",
        };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var result = await _camera.CaptureImageAsync(dialog.FileName);
            if (result.IsSuccess)
                _logger.Information("[{Camera}] 图像已保存: {Path}", _cameraName, result.Data);
            else
                _logger.Warning("[{Camera}] 保存 BMP 失败: {Msg}", _cameraName, result.Message);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[{Camera}] 保存 BMP 失败", _cameraName);
        }
    }

    /// <summary>原始帧 → HImage（单色直接构造；彩色 BGR 加权转灰度）</summary>
    private static unsafe HImage BuildHImage(byte[] data, int w, int h, bool isMono)
    {
        if (isMono)
        {
            fixed (byte* p = data)
                return new HImage("byte", w, h, (IntPtr)p);
        }

        var gray = new byte[w * h];
        for (int i = 0; i < w * h; i++)
        {
            int src = i * 3;
            gray[i] = (byte)((data[src + 2] * 76 + data[src + 1] * 150 + data[src] * 30) >> 8);
        }
        fixed (byte* p = gray)
            return new HImage("byte", w, h, (IntPtr)p);
    }

    // ==================== 视觉结果绘制 ====================

    public Task HandleAsync(VisionInspectionMessage message, CancellationToken cancellationToken)
    {
        if (!string.Equals(message.CameraName, _cameraName, StringComparison.Ordinal))
            return Task.CompletedTask;

        DrawInspectionResult(message);
        return Task.CompletedTask;
    }

    private void DrawInspectionResult(VisionInspectionMessage message)
    {
        var result = message.Result;
        var window = _halconWindow;

        // 底图：当前最新实时帧；窗口未挂接时仅更新结果文本
        if (window != null && _displayImage != null)
            window.DispObj(_displayImage);

        if (window != null)
        {
            if (result.NccSuccess)
                DrawNccResult(window, result, message.ModelPath);

            if (result.Edge1Success)
                DrawEdgeResult(window,
                    result.Edge1ResultStartX, result.Edge1ResultStartY,
                    result.Edge1ResultEndX, result.Edge1ResultEndY, "green");

            if (result.Edge2Success)
                DrawEdgeResult(window,
                    result.Edge2ResultStartX, result.Edge2ResultStartY,
                    result.Edge2ResultEndX, result.Edge2ResultEndY, "yellow");

            if (result.PointSuccess)
                DrawPointResult(window, result.PointResultX, result.PointResultY);
        }

        LastResultText = BuildResultText(result);
    }

    private void DrawNccResult(HWindow window, VisionInspectionResult result, string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            return;

        try
        {
            HOperatorSet.ReadShapeModel(modelPath, out HTuple modelId);
            HOperatorSet.GetShapeModelContours(out HObject modelContours, modelId, 1);
            HOperatorSet.VectorAngleToRigid(
                0, 0, 0,
                result.NccResultRow, result.NccResultColumn, result.NccResultAngle * Math.PI / 180.0,
                out HTuple homMat2D);
            HOperatorSet.AffineTransContourXld(modelContours, out HObject transContours, homMat2D);

            window.SetColor("red");
            window.SetLineWidth(2);
            window.DispObj(transContours);

            modelContours.Dispose();
            transContours.Dispose();
            modelId.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[{Camera}] 绘制 NCC 轮廓失败", _cameraName);
        }
    }

    private static void DrawEdgeResult(HWindow window,
        double startX, double startY, double endX, double endY, string color)
    {
        window.SetColor(color);
        window.SetLineWidth(2);
        window.DispLine(startY, startX, endY, endX);
    }

    private static void DrawPointResult(HWindow window, double x, double y)
    {
        const int crossSize = 10;
        window.SetColor("red");
        window.SetLineWidth(1);
        window.DispLine(y - crossSize, x, y + crossSize, x);
        window.DispLine(y, x - crossSize, y, x + crossSize);
    }

    private static string BuildResultText(VisionInspectionResult result)
    {
        var parts = new List<string>(4);
        if (result.NccSuccess)
            parts.Add($"NCC ΔX={result.Dx:+0.00;-0.00} ΔY={result.Dy:+0.00;-0.00}");
        else
            parts.Add("NCC 未匹配");

        if (result.Edge1Success)
            parts.Add($"边1 {result.Edge1AngleDev:+0.00°;-0.00°}");
        else
            parts.Add("边1 失败");

        if (result.Edge2Success)
            parts.Add($"边2 {result.Edge2AngleDev:+0.00°;-0.00°}");
        else
            parts.Add("边2 失败");

        parts.Add(result.PointSuccess
            ? $"交点 ΔX={result.PointDevX:+0.00;-0.00} ΔY={result.PointDevY:+0.00;-0.00}"
            : "找点 失败");

        return string.Join(" | ", parts);
    }
}

// ==================== 4 个相机面板 ====================

[Export]
[Export(typeof(ILeftUpCameraTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class LeftUpCameraViewModel(
    [ImportMany] IEnumerable<ICamera> cameras,
    ILogger logger,
    IEventAggregator events,
    IToastService toastService)
    : CameraToolViewModelBase(ResolveCamera(cameras, "左上相机"), "左上相机", logger, events, toastService), ILeftUpCameraTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 640;
    public override double PreferredHeight => 520;

    private static ICamera ResolveCamera(IEnumerable<ICamera> cameras, string name)
        => cameras.First(c => c.GetType().GetDescription() == name);
}

[Export]
[Export(typeof(ILeftDownCameraTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class LeftDownCameraViewModel(
    [ImportMany] IEnumerable<ICamera> cameras,
    ILogger logger,
    IEventAggregator events,
    IToastService toastService)
    : CameraToolViewModelBase(ResolveCamera(cameras, "左下相机"), "左下相机", logger, events, toastService), ILeftDownCameraTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 640;
    public override double PreferredHeight => 520;

    private static ICamera ResolveCamera(IEnumerable<ICamera> cameras, string name)
        => cameras.First(c => c.GetType().GetDescription() == name);
}

[Export]
[Export(typeof(IRightUpCameraTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class RightUpCameraViewModel(
    [ImportMany] IEnumerable<ICamera> cameras,
    ILogger logger,
    IEventAggregator events,
    IToastService toastService)
    : CameraToolViewModelBase(ResolveCamera(cameras, "右上相机"), "右上相机", logger, events, toastService), IRightUpCameraTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 640;
    public override double PreferredHeight => 520;

    private static ICamera ResolveCamera(IEnumerable<ICamera> cameras, string name)
        => cameras.First(c => c.GetType().GetDescription() == name);
}

[Export]
[Export(typeof(IRightDownCameraTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class RightDownCameraViewModel(
    [ImportMany] IEnumerable<ICamera> cameras,
    ILogger logger,
    IEventAggregator events,
    IToastService toastService)
    : CameraToolViewModelBase(ResolveCamera(cameras, "右下相机"), "右下相机", logger, events, toastService), IRightDownCameraTool
{
    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 640;
    public override double PreferredHeight => 520;

    private static ICamera ResolveCamera(IEnumerable<ICamera> cameras, string name)
        => cameras.First(c => c.GetType().GetDescription() == name);
}
