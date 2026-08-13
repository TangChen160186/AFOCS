using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using AFOCS.App.Models;
using AFOCS.App.Services;
using AFOCS.Devices.AkribrisMotion;
using AFOCS.Devices.BusAxisDevice;
using AFOCS.Devices.Camera;
using AFOCS.Devices.HeightGauge;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using AFOCS.VisionEditor.Models;
using AFOCS.VisionEditor.Services;
using Caliburn.Micro;
using Microsoft.Win32;
using Serilog;

namespace AFOCS.App.ViewModels;

public interface IFaPdCalibration : ITool;

/// <summary>
/// FA 下表面到 PD 测高的标定界面（全局只标定一次）。
/// 流程：移动到标定示教点 → 读取轴位置(P0)/测高值(H0) → 采集图像并视觉找点得到像素(Y0) → 保存标定配置。
/// </summary>
[Export]
[Export(typeof(IFaPdCalibration))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class FaPdCalibrationViewModel(
    IToastService toastService,
    IConfigService configService,
    IHeightGauge heightGauge,
    IBusAxisDevice busAxisDevice,
    [ImportMany] IEnumerable<IAkribisMotion> akribisMotions,
    [ImportMany] IEnumerable<ICamera> cameras,
    ILogger logger) : Tool, IFaPdCalibration
{
    private readonly IToastService _toastService = toastService;
    private readonly IConfigService _configService = configService;
    private readonly IHeightGauge _heightGauge = heightGauge;
    private readonly IBusAxisDevice _busAxisDevice = busAxisDevice;
    private readonly ILogger _logger = logger;
    private readonly Dictionary<string, IAkribisMotion> _akribisInstances = [];
    private readonly Dictionary<string, ICamera> _cameraMap = [];

    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 420;
    public override double PreferredHeight => 600;

    public override string DisplayName => "FA下表面PD测高标定";

    // ========== 基础配置 ==========

    public ObservableCollection<string> CameraNames { get; } = [];

    public ObservableCollection<TeachingPointPoco> TeachingPoints { get; } = [];

    private WorkPos _selectedStation = WorkPos.Left;
    public WorkPos SelectedStation
    {
        get => _selectedStation;
        set => Set(ref _selectedStation, value);
    }

    private EAxis _selectedAxis = EAxis.CouplingLZ;
    public EAxis SelectedAxis
    {
        get => _selectedAxis;
        set => Set(ref _selectedAxis, value);
    }

    private TeachingPointPoco? _selectedPoint;
    public TeachingPointPoco? SelectedPoint
    {
        get => _selectedPoint;
        set => Set(ref _selectedPoint, value);
    }

    private string _selectedCameraName = string.Empty;
    public string SelectedCameraName
    {
        get => _selectedCameraName;
        set => Set(ref _selectedCameraName, value);
    }

    private string _templatePath = string.Empty;
    public string TemplatePath
    {
        get => _templatePath;
        set => Set(ref _templatePath, value);
    }

    public IReadOnlyList<int> Channels { get; } = [1, 2, 3, 4];

    private int _channel = 1;
    public int Channel
    {
        get => _channel;
        set => Set(ref _channel, value);
    }

    // ========== 状态 ==========

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    private string _statusMessage = "就绪";
    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    // ========== 标定结果 ==========

    private bool _isCalibrated;
    public bool IsCalibrated
    {
        get => _isCalibrated;
        set => Set(ref _isCalibrated, value);
    }

    private double _calibAxisPosition;
    public double CalibAxisPosition
    {
        get => _calibAxisPosition;
        set => Set(ref _calibAxisPosition, value);
    }

    private double _calibHeight;
    public double CalibHeight
    {
        get => _calibHeight;
        set => Set(ref _calibHeight, value);
    }

    private double _calibPixelX;
    public double CalibPixelX
    {
        get => _calibPixelX;
        set => Set(ref _calibPixelX, value);
    }

    private double _calibPixelY;
    public double CalibPixelY
    {
        get => _calibPixelY;
        set => Set(ref _calibPixelY, value);
    }

    private double _calibPrecision;
    public double CalibPrecision
    {
        get => _calibPrecision;
        set => Set(ref _calibPrecision, value);
    }

    // ========== 构造 ==========

    protected override async Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        foreach (var motion in akribisMotions)
            _akribisInstances[motion.GetType().Name] = motion;

        foreach (var camera in cameras)
        {
            var name = camera.GetType().GetDescription();
            _cameraMap[name] = camera;
            CameraNames.Add(name);
        }

        await LoadTeachingPointsAsync();
        await LoadExistingCalibrationAsync();

        await base.OnInitializedAsync(cancellationToken);
    }

    // ========== 加载 ==========

    public async Task LoadTeachingPointsAsync()
    {
        try
        {
            var config = await _configService.LoadAsync<TeachingPointsConfig>();
            TeachingPoints.Clear();
            if (config?.Points != null)
            {
                foreach (var point in config.Points)
                    TeachingPoints.Add(point);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "加载示教点失败");
        }
    }

    private async Task LoadExistingCalibrationAsync()
    {
        var calib = await _configService.LoadAsync<FaPdCalibrationConfig>();
        if (calib == null || !calib.IsCalibrated) return;

        SelectedStation = calib.Station;
        SelectedAxis = calib.Axis;
        SelectedCameraName = calib.CameraName;
        TemplatePath = calib.TemplatePath;

        CalibAxisPosition = calib.AxisPosition;
        CalibHeight = calib.HeightValue;
        CalibPixelX = calib.PixelX;
        CalibPixelY = calib.PixelY;
        CalibPrecision = calib.Precision;
        IsCalibrated = true;

        StatusMessage = "已加载历史标定值";
    }

    // ========== 操作 ==========

    public void BrowseTemplate()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "视觉模板文件 (*.vtemplate)|*.vtemplate|所有文件 (*.*)|*.*",
            Title = "选择视觉模板",
        };
        if (dlg.ShowDialog() == true)
            TemplatePath = dlg.FileName;
    }

    public async Task MoveToTeachingPoint()
    {
        if (SelectedPoint == null)
        {
            _toastService.ShowWarning("请先选择示教点");
            return;
        }

        var point = SelectedPoint;
        var axisKeys = point.AxisKeys;
        var positions = point.AxisPositions;

        if (axisKeys.Count == 0)
        {
            _toastService.ShowWarning("该示教点没有关联轴");
            return;
        }

        IsBusy = true;
        StatusMessage = "运动中...";
        try
        {
            var tasks = axisKeys
                .Where(positions.ContainsKey)
                .Select(axis => MoveSingleAxisAsync(axis, positions[axis], point.Station))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            var errors = results.Where(r => r != null).ToList();

            StatusMessage = errors.Count == 0
                ? $"已到达示教点 \"{point.Name}\""
                : $"部分完成，{errors.Count} 个轴失败: {string.Join("; ", errors)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"运动异常: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CalibrateAsync()
    {
        if (!_cameraMap.TryGetValue(SelectedCameraName, out var camera))
        {
            _toastService.ShowWarning("请先选择相机");
            return;
        }
        if (string.IsNullOrWhiteSpace(TemplatePath) || !File.Exists(TemplatePath))
        {
            _toastService.ShowWarning("请先选择有效的视觉模板文件");
            return;
        }

        IsBusy = true;
        StatusMessage = "正在标定...";
        try
        {
            // 1. 读当前轴位置 P0
            double axisPos = await ReadAxisPositionAsync();

            // 2. 读测高值 H0
            var hResult = await _heightGauge.GetHeightAsync(Channel);
            if (!hResult.IsSuccess)
                throw new InvalidOperationException($"读取测高仪失败: {hResult.Message}");
            double h0 = hResult.Data;

            // 3. 采集图像并视觉找点，得到像素 Y0
            var (pixelX, pixelY) = await DetectPointAsync(camera);

            // 4. 读相机精度
            double precision = camera.GetConfig().Precision;

            // 5. 保存标定配置
            var calib = new FaPdCalibrationConfig
            {
                IsCalibrated = true,
                Station = SelectedStation,
                Axis = SelectedAxis,
                AxisPosition = axisPos,
                HeightValue = h0,
                PixelX = pixelX,
                PixelY = pixelY,
                Precision = precision,
                CameraName = SelectedCameraName,
                TemplatePath = TemplatePath,
            };
            await _configService.SaveAsync(calib);

            CalibAxisPosition = axisPos;
            CalibHeight = h0;
            CalibPixelX = pixelX;
            CalibPixelY = pixelY;
            CalibPrecision = precision;
            IsCalibrated = true;

            StatusMessage = "标定成功";
            _toastService.ShowInfo("标定完成");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "标定失败");
            StatusMessage = $"标定失败: {ex.Message}";
            _toastService.ShowError($"标定失败: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ========== 辅助 ==========

    private async Task<double> ReadAxisPositionAsync()
    {
        if (SelectedAxis.IsBusAxis())
        {
            var busId = SelectedAxis.ToBusAxisId(SelectedStation);
            var result = await _busAxisDevice.GetPositionAsync(busId);
            if (!result.IsSuccess)
                throw new InvalidOperationException($"读取轴位置失败: {result.Message}");
            return result.Data;
        }

        if (SelectedAxis.IsAkribisAxis())
        {
            var (instanceName, akAxis) = SelectedAxis.ToAkribis(SelectedStation);
            if (!_akribisInstances.TryGetValue(instanceName, out var motion))
                throw new InvalidOperationException($"未找到控制器 {instanceName}");

            return akAxis switch
            {
                AkribisAxisId.X => motion.PositionX,
                AkribisAxisId.Y => motion.PositionY,
                AkribisAxisId.Z => motion.PositionZ,
                _ => throw new InvalidOperationException($"未知雅克贝斯轴 {akAxis}"),
            };
        }

        throw new InvalidOperationException($"未知轴类型: {SelectedAxis.GetDescription()}");
    }

    private async Task<(double PixelX, double PixelY)> DetectPointAsync(ICamera camera)
    {
        var frame = await camera.GrabFrameAsync();
        if (!frame.IsSuccess)
            throw new InvalidOperationException($"采集图像失败: {frame.Message}");

        var (data, width, height, isMono) = frame.Data;

        byte[] pixelData;
        int channels;
        if (isMono)
        {
            pixelData = data;
            channels = 1;
        }
        else
        {
            channels = 1;
            int total = width * height;
            pixelData = new byte[total];
            for (int i = 0; i < total; i++)
            {
                int src = i * 3;
                byte b = data[src];
                byte g = data[src + 1];
                byte r = data[src + 2];
                pixelData[i] = (byte)((r * 76 + g * 150 + b * 30) >> 8);
            }
        }

        var template = JsonHelper.Deserialize<VisionTemplate>(await File.ReadAllTextAsync(TemplatePath));
        using var hImage = new PixelData(pixelData, width, height, channels).ToHImage();

        var result = new VisionInspectionService().Inspect(hImage, template)
            ?? throw new InvalidOperationException("视觉检测返回 null");

        if (!result.PointSuccess)
            throw new InvalidOperationException("视觉找点失败，请检查模板与图像");

        return (result.PointResultX, result.PointResultY);
    }

    private async Task<string?> MoveSingleAxisAsync(EAxis axis, double targetPos, WorkPos station)
    {
        try
        {
            if (axis.IsBusAxis())
            {
                var busId = axis.ToBusAxisId(station);
                var moveResult = await _busAxisDevice.MovePmoveAsync(busId, targetPos, posiMode: 1);
                return moveResult.IsSuccess ? null : $"{axis.GetDescription()}: {moveResult.Message}";
            }

            if (axis.IsAkribisAxis())
            {
                var (instanceName, akAxis) = axis.ToAkribis(station);
                if (!_akribisInstances.TryGetValue(instanceName, out var motion))
                    return $"{axis.GetDescription()}: 未找到控制器 {instanceName}";

                var akResult = await motion.MoveAbsAsync(akAxis, (int)targetPos);
                return akResult.IsSuccess ? null : $"{axis.GetDescription()}: {akResult.Message}";
            }

            return $"{axis.GetDescription()}: 未知轴类型";
        }
        catch (Exception ex)
        {
            return $"{axis.GetDescription()}: {ex.Message}";
        }
    }
}
