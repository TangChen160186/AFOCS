using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Windows;
using AFOCS.App.Models;
using AFOCS.App.Services;
using AFOCS.Devices;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;

namespace AFOCS.App.ViewModels;

public interface ITeachingPointTest : ITool;

[Export]
[Export(typeof(ITeachingPointTest))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class TeachingPointTestViewModel(
    IToastService toastService,
    IBusAxisDevice busAxisDevice,
    IConfigService configService,
    [ImportMany] IEnumerable<IAkribisMotion> akribisMotions) : Tool, ITeachingPointTest
{
    private readonly IToastService _toastService = toastService;
    private readonly IBusAxisDevice _busAxisDevice = busAxisDevice;
    private readonly IConfigService _configService = configService;
    private readonly Dictionary<string, IAkribisMotion> _akribisInstances = [];

    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 400;
    public override double PreferredHeight => 500;

    public override string DisplayName => "示教点测试";

    // ========== 示教点列表 ==========

    public ObservableCollection<TeachingPointPoco> TeachingPoints { get; } = [];

    private TeachingPointPoco? _selectedPoint;
    public TeachingPointPoco? SelectedPoint
    {
        get => _selectedPoint;
        set
        {
            if (Set(ref _selectedPoint, value))
            {
                NotifyOfPropertyChange(nameof(IsPointSelected));
                NotifyOfPropertyChange(nameof(PointAxisInfo));
            }
        }
    }

    public bool IsPointSelected => _selectedPoint != null;

    public string PointAxisInfo
    {
        get
        {
            if (_selectedPoint == null) return string.Empty;
            var names = _selectedPoint.AxisKeys.Select(k => k.GetDescription());
            return $"工位: {_selectedPoint.Station.GetDescription()} | 轴数: {_selectedPoint.AxisKeys.Count} | {string.Join(", ", names)}";
        }
    }

    public string MoveStatus
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    public bool IsMoving
    {
        get;
        set => Set(ref field, value);
    }

    // ========== 构造 ==========

    protected override Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        foreach (var motion in akribisMotions)
            _akribisInstances[motion.GetType().Name] = motion;
        LoadTeachingPointsAsync();
        return base.OnInitializedAsync(cancellationToken);
    }



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
            MoveStatus = TeachingPoints.Count > 0
                ? $"已加载 {TeachingPoints.Count} 个示教点"
                : "暂无示教点";
            NotifyOfPropertyChange(nameof(MoveStatus));
        }
        catch (Exception ex)
        {
            MoveStatus = $"加载失败: {ex.Message}";
            NotifyOfPropertyChange(nameof(MoveStatus));
        }
    }

    // ========== 运动到示教点 ==========

    public async Task MoveToTeachingPoint()
    {
        if (_selectedPoint == null)
        {
            _toastService.ShowWarning("请先选择示教点");
            return;
        }

        var point = _selectedPoint;
        var axisKeys = point.AxisKeys;
        var positions = point.AxisPositions;
        var station = point.Station;

        if (axisKeys.Count == 0)
        {
            _toastService.ShowWarning("该示教点没有关联轴");
            return;
        }

        var result = MessageBox.Show(
            $"确定要运动到示教点 \"{point.Name}\" 吗？\n\n工位: {station.GetDescription()}\n轴数: {axisKeys.Count}",
            "运动确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        IsMoving = true;
        MoveStatus = "运动中...";
        NotifyOfPropertyChange(nameof(MoveStatus));
        NotifyOfPropertyChange(nameof(IsMoving));

        try
        {
            var tasks = axisKeys
                .Where(positions.ContainsKey)
                .Select(axis => MoveSingleAxisAsync(axis, positions[axis], station))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            var errors = results.Where(r => r != null).ToList();

            MoveStatus = errors.Count == 0
                ? $"已到达示教点 \"{point.Name}\""
                : $"部分完成，{errors.Count} 个轴失败: {string.Join("; ", errors)}";
        }
        catch (Exception ex)
        {
            MoveStatus = $"运动异常: {ex.Message}";
        }
        finally
        {
            IsMoving = false;
            NotifyOfPropertyChange(nameof(MoveStatus));
            NotifyOfPropertyChange(nameof(IsMoving));
        }
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
