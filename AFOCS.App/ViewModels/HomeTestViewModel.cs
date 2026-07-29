using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Windows;
using AFOCS.App.Models;
using AFOCS.App.Services;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels;

public interface IHomeTest : ITool;

/// <summary>轴回零项（绑定到列表）</summary>
public class HomeAxisItem : PropertyChangedBase, IDisposable
{
    private readonly Func<Task<Result>> _homeAction;

    public string Name { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    private bool _isHoming;
    public bool IsHoming
    {
        get => _isHoming;
        set => Set(ref _isHoming, value);
    }

    private bool _isDone;
    public bool IsDone
    {
        get => _isDone;
        set => Set(ref _isDone, value);
    }

    public HomeAxisItem(string name, Func<Task<Result>> homeAction)
    {
        Name = name;
        _homeAction = homeAction;
    }

    public async Task Home()
    {
        IsHoming = true;
        IsDone = false;
        try
        {
            var result = await _homeAction();
            IsDone = result.IsSuccess;
        }
        finally
        {
            IsHoming = false;
        }
    }

    public void Dispose() { }
}

[Export]
[Export(typeof(IHomeTest))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class HomeTestViewModel(
    IToastService toastService,
    IBusAxisDevice busAxisDevice,
    [ImportMany] IEnumerable<IAkribisMotion> akribisMotions) : Tool, IHomeTest
{
    private readonly IToastService _toastService = toastService;
    private readonly IBusAxisDevice _busAxisDevice = busAxisDevice;
    private readonly Dictionary<string, IAkribisMotion> _akribisInstances = [];

    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 400;
    public override double PreferredHeight => 600;

    public override string DisplayName => "回零测试";

    // ========== 工位 ==========

    private WorkPos _selectedStation = WorkPos.Left;
    public WorkPos SelectedStation
    {
        get => _selectedStation;
        set
        {
            if (Set(ref _selectedStation, value))
            {
                BuildBusAxes();
                NotifyOfPropertyChange(nameof(HasSelection));
            }
        }
    }

    // ========== 轴列表（总线 + 雅克贝斯） ==========

    public ObservableCollection<HomeAxisItem> BusAxes { get; } = [];
    public ObservableCollection<HomeAxisItem> AkAxes { get; } = [];

    public bool HasSelection => BusAxes.Any(a => a.IsSelected) || AkAxes.Any(a => a.IsSelected);

    // ========== 状态 ==========

    public string StatusText
    {
        get;
        set => Set(ref field, value);
    } = "就绪";

    public bool IsHoming
    {
        get;
        set => Set(ref field, value);
    }

    // ========== 构造 ==========

    protected override Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        foreach (var motion in akribisMotions)
            _akribisInstances[motion.GetType().Name] = motion;
        BuildBusAxes();
        BuildAkAxes();
        return base.OnInitializedAsync(cancellationToken);
    }

    private void BuildBusAxes()
    {
        BusAxes.Clear();

        var busAxes = new[]
        {
            ("上相机X", EAxis.CamUpX),
            ("上相机Y", EAxis.CamUpY),
            ("上相机Z", EAxis.CamUpZ),
            ("侧相机Y", EAxis.CamSideY),
            ("左耦合θX", EAxis.CouplingLThetaX),
            ("左耦合θY", EAxis.CouplingLThetaY),
            ("左耦合θZ", EAxis.CouplingLThetaZ),
            ("右耦合θX", EAxis.CouplingRThetaX),
            ("右耦合θY", EAxis.CouplingRThetaY),
            ("右耦合θZ", EAxis.CouplingRThetaZ),
        };

        foreach (var (name, axis) in busAxes)
        {
            BusAxes.Add(new HomeAxisItem(name, () =>
            {
                var busId = axis.ToBusAxisId(SelectedStation);
                return _busAxisDevice.MoveHomeAsync(busId);
            }));
        }
    }

    private void BuildAkAxes()
    {
        AkAxes.Clear();

        var akPairs = new[]
        {
            ("左耦合L", nameof(AkribisLeftCouplingL)),
            ("左耦合R", nameof(AkribisLeftCouplingR)),
            ("右耦合L", nameof(AkribisRightCouplingL)),
            ("右耦合R", nameof(AkribisRightCouplingR)),
        };

        foreach (var (label, instanceName) in akPairs)
        {
            if (!_akribisInstances.TryGetValue(instanceName, out var motion)) continue;

            var akAxes = new[] { (AkribisAxisId.X, "X"), (AkribisAxisId.Y, "Y"), (AkribisAxisId.Z, "Z") };
            foreach (var (akAxis, axisLabel) in akAxes)
            {
                AkAxes.Add(new HomeAxisItem($"{label}.{axisLabel}", () =>
                    motion.HomeAsync(akAxis)));
            }
        }
    }

    private void UpdateStatus(string text)
    {
        StatusText = text;
        NotifyOfPropertyChange(nameof(StatusText));
    }

    // ========== 勾选变化 ==========

    public void OnSelectionChanged()
    {
        NotifyOfPropertyChange(nameof(HasSelection));
    }

    // ========== 回零选中轴 ==========

    public async Task HomeSelected()
    {
        var selected = BusAxes.Where(a => a.IsSelected)
            .Concat(AkAxes.Where(a => a.IsSelected))
            .ToList();

        if (selected.Count == 0)
        {
            _toastService.ShowWarning("请先勾选需要回零的轴");
            return;
        }

        var result = MessageBox.Show(
            $"确定要回零 {selected.Count} 个轴吗？",
            "回零确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        IsHoming = true;
        NotifyOfPropertyChange(nameof(IsHoming));
        UpdateStatus($"正在回零 {selected.Count} 个轴...");

        var tasks = selected.Select(a => a.Home()).ToArray();
        await Task.WhenAll(tasks);

        IsHoming = false;
        NotifyOfPropertyChange(nameof(IsHoming));

        var ok = selected.Count(a => a.IsDone);
        var failed = selected.Count - ok;
        UpdateStatus($"完成: {ok}/{selected.Count}" + (failed > 0 ? $", 失败: {failed}" : ""));
    }
}
