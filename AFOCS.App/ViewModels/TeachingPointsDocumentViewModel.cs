using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Windows;
using AFOCS.App.Models;
using AFOCS.Devices.AkribrisMotion;
using AFOCS.Devices.BusAxisDevice;
using AFOCS.Framework.Framework;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Caliburn.Micro;
using GongSolutions.Wpf.DragDrop;
using Action = System.Action;

namespace AFOCS.App.ViewModels;


// ==================== UI 绑定辅助类型 ====================

public class AxisSelectionItem : PropertyChangedBase
{
    public required EAxis Axis { get; init; }
    public string DisplayName => Axis.GetDescription();

    /// <summary>勾选状态变化回调（用于自动更新示教点轴列表）</summary>
    public Action? OnSelectionChanged { get; set; }

    public bool IsSelected
    {
        get;
        set
        {
            if (Set(ref field, value))
                OnSelectionChanged?.Invoke();
        }
    }
}

public class AxisGroupItem
{
    public string GroupName { get; set; } = string.Empty;
    public ObservableCollection<AxisSelectionItem> Axes { get; set; } = [];
}

public class TeachingPointItem : PropertyChangedBase
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    public WorkPos Station { get; set; } = WorkPos.Left;
}

public class AxisPositionItem : PropertyChangedBase
{
    public Action? OnChanged { get; set; }

    public required EAxis Axis { get; init; }
    public string DisplayName => Axis.GetDescription();

    public double Position
    {
        get;
        set
        {
            if (Set(ref field, value))
                OnChanged?.Invoke();
        }
    }
}

// ==================== Document ViewModel ====================

[Export]
public class TeachingPointsDocumentViewModel : Document, IDropTarget
{
    public override string DisplayName { get; set; } = "示教点";

    private readonly IConfigService _configService;
    private readonly IBusAxisDevice _busAxisDevice;
    private readonly Dictionary<string, IAkribisMotion> _akribisInstances = [];

    // ========== 工位 ==========

    private WorkPos _selectedStation = WorkPos.Left;
    public WorkPos SelectedStation
    {
        get => _selectedStation;
        set
        {
            if (Set(ref _selectedStation, value))
            {
                NotifyOfPropertyChange(nameof(StationPoints));
                SelectedTeachingPoint = null;
                ResetCheckboxesToDefault();
            }
        }
    }

    // ========== 轴列表（静态，16 个角色与工位无关）==========

    public ObservableCollection<AxisSelectionItem> AllAxes { get; } = [];
    public ObservableCollection<AxisGroupItem> StationAxisGroups { get; } = [];



    // ========== 示教点列表 ==========

    public ObservableCollection<TeachingPointItem> AllPoints { get; } = [];

    public IEnumerable<TeachingPointItem> StationPoints =>
        AllPoints.Where(p => p.Station == SelectedStation);

    private TeachingPointItem? _selectedTeachingPoint;
    public TeachingPointItem? SelectedTeachingPoint
    {
        get => _selectedTeachingPoint;
        set
        {
            if (_selectedTeachingPoint == value) return;
            SaveCurrentPointData();
            _selectedTeachingPoint = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(IsPointSelected));
            NotifyOfPropertyChange(nameof(PointName));
            LoadPointData(value);

            if (value == null)
                ResetCheckboxesToDefault();
            else
                SyncCheckboxesToPoint(value);
        }
    }

    public bool IsPointSelected => _selectedTeachingPoint != null;

    public string PointName
    {
        get => _selectedTeachingPoint?.Name ?? string.Empty;
        set
        {
            if (_selectedTeachingPoint == null || _selectedTeachingPoint.Name == value) return;
            _selectedTeachingPoint.Name = value;
            NotifyOfPropertyChange();
            IsModified = true;
        }
    }

    // ========== 当前编辑点的轴位置 ==========

    public ObservableCollection<AxisPositionItem> CurrentPositions { get; } = [];

    public bool IsModified
    {
        get;
        set => Set(ref field, value);
    }

    public string StatusMessage
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    public bool IsBusy
    {
        get;
        set => Set(ref field, value);
    }

    // pointId → 轴顺序
    private readonly Dictionary<Guid, List<EAxis>> _pointAxisKeys = [];
    // pointId → (EAxis → position)
    private readonly Dictionary<Guid, Dictionary<EAxis, double>> _pointData = [];

    private bool _isSyncingAxes;

    // ========== 构造 ==========

    [ImportingConstructor]
    public TeachingPointsDocumentViewModel(
        IConfigService configService,
        IBusAxisDevice busAxisDevice,
        [ImportMany] IEnumerable<IAkribisMotion> akribisMotions)
    {
        _configService = configService;
        _busAxisDevice = busAxisDevice;
        foreach (var motion in akribisMotions)
            _akribisInstances[motion.GetType().Name] = motion;

        InitializeAxes();
        _ = LoadAsync();
    }

    #region 初始化轴列表

    private void InitializeAxes()
    {
        AxisGroupItem _camGroup = new() { GroupName = "相机轴（总线）", Axes = [] };
        AxisGroupItem _thetaGroup = new() { GroupName = "耦合旋转轴（总线）", Axes = [] };
        AxisGroupItem _linearGroup = new() { GroupName = "耦合直线轴（雅克贝斯）", Axes = [] };
        // 相机轴 0–3
        foreach (var axis in Enum.GetValues<EAxis>().Take(4))
        {
            var item = new AxisSelectionItem()
            {
                Axis = axis,
                OnSelectionChanged = OnAxisSelectionChanged,
            };
            AllAxes.Add(item);
            _camGroup.Axes.Add(item);
        }

        // 耦合旋转轴 4–9
        foreach (var axis in Enum.GetValues<EAxis>().Skip(4).Take(6))
        {
            var item = new AxisSelectionItem()
            {
                Axis = axis,
                OnSelectionChanged = OnAxisSelectionChanged,
            };
            AllAxes.Add(item);
            _thetaGroup.Axes.Add(item);
        }

        // 耦合直线轴 10–15
        foreach (var axis in Enum.GetValues<EAxis>().Skip(10).Take(6))
        {
            var item = new AxisSelectionItem()
            {
                Axis = axis,
                OnSelectionChanged = OnAxisSelectionChanged,
            };
            AllAxes.Add(item);
            _linearGroup.Axes.Add(item);
        }
        StationAxisGroups.Add(_camGroup);
        StationAxisGroups.Add(_thetaGroup);
        StationAxisGroups.Add(_linearGroup);
    }

    #endregion



    // ========== 复选框 ↔ 示教点 ==========
    private void SyncCheckboxesToPoint(TeachingPointItem point)
    {
        _isSyncingAxes = true;
        var keys = _pointAxisKeys.TryGetValue(point.Id, out var axisKeys) ? axisKeys : [];
        var keySet = new HashSet<EAxis>(keys);
        foreach (var axis in AllAxes)
            axis.IsSelected = keySet.Contains(axis.Axis);
        _isSyncingAxes = false;
    }

    private void ResetCheckboxesToDefault()
    {
        _isSyncingAxes = true;
        foreach (var axis in AllAxes)
            axis.IsSelected = axis.Axis.IsBusAxis() && axis.Axis >= EAxis.CouplingLThetaX;
        _isSyncingAxes = false;
    }

    private void OnAxisSelectionChanged()
    {
        if (_isSyncingAxes || _selectedTeachingPoint == null) return;

        var newKeys = AllAxes.Where(a => a.IsSelected).Select(a => a.Axis).ToList();
        if (newKeys.Count == 0) return;

        SaveCurrentPointData();

        var id = _selectedTeachingPoint.Id;
        _pointAxisKeys[id] = newKeys;

        var positions = _pointData[id];
        foreach (var key in newKeys)
        {
            positions.TryAdd(key, 0);
        }
        var toRemove = positions.Keys.Except(newKeys).ToList();
        foreach (var key in toRemove)
            positions.Remove(key);

        LoadPointData(_selectedTeachingPoint);
        IsModified = true;
        StatusMessage = $"已更新轴列表: {newKeys.Count} 个轴";
    }

    // ========== 加载 / 保存 ==========

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "加载中...";
        try
        {
            var config = await _configService.LoadAsync<TeachingPointsConfig>();
            if (config == null)
            {
                StatusMessage = "就绪";
                return;
            }

            _pointData.Clear();
            _pointAxisKeys.Clear();
            AllPoints.Clear();

            foreach (var point in config.Points)
            {
                var id = point.Id != Guid.Empty ? point.Id : Guid.NewGuid();
                _pointData[id] = new Dictionary<EAxis, double>(point.AxisPositions);
                _pointAxisKeys[id] = point.AxisKeys;
                AllPoints.Add(new TeachingPointItem { Id = id, Name = point.Name, Station = point.Station });
            }

            NotifyOfPropertyChange(nameof(StationPoints));
            StatusMessage = $"已加载 {AllPoints.Count} 个示教点";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsModified = false;
        }
    }

    public async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = "保存中...";
        try
        {
            SaveCurrentPointData();

            var config = new TeachingPointsConfig
            {
                Points = AllPoints.Select(p => new TeachingPointPoco
                {
                    Id = p.Id,
                    Name = p.Name,
                    Station = p.Station,
                    AxisKeys = _pointAxisKeys.TryGetValue(p.Id, out var keys) ? keys : [],
                    AxisPositions = _pointData.TryGetValue(p.Id, out var pos) ? pos : [],
                }).ToList(),
            };

            if (await _configService.SaveAsync(config))
            {
                IsModified = false;
                StatusMessage = $"已保存 {config.Points.Count} 个示教点";
            }
            else
            {
                StatusMessage = "保存失败";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ========== 示教点增删 ==========

    public void AddTeachingPoint()
    {
        var baseName = $"{SelectedStation.GetDescription()}示教点";
        var name = baseName;
        var idx = 1;
        while (AllPoints.Any(p => p.Name == name))
            name = $"{baseName}_{++idx}";

        var id = Guid.NewGuid();
        var item = new TeachingPointItem { Id = id, Name = name, Station = SelectedStation };
        AllPoints.Add(item);

        _pointAxisKeys[id] = [];
        _pointData[id] = [];

        NotifyOfPropertyChange(nameof(StationPoints));
        SelectedTeachingPoint = item;
        IsModified = true;
        StatusMessage = $"已添加: {name}";
    }

    public void DeleteTeachingPoint()
    {
        if (_selectedTeachingPoint == null) return;

        var name = _selectedTeachingPoint.Name;
        var result = MessageBox.Show(
            $"确定要删除示教点 \"{name}\" 吗？此操作不可撤销。",
            "删除确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        _pointData.Remove(_selectedTeachingPoint.Id);
        _pointAxisKeys.Remove(_selectedTeachingPoint.Id);
        AllPoints.Remove(_selectedTeachingPoint);
        SelectedTeachingPoint = null;
        NotifyOfPropertyChange(nameof(StationPoints));
        IsModified = true;
        StatusMessage = $"已删除: {name}";
    }

    // ========== 读取当前坐标 ==========

    public async Task ReadCurrentPosition()
    {
        if (_selectedTeachingPoint == null)
        {
            StatusMessage = "请先选择要读取的示教点";
            return;
        }

        var name = _selectedTeachingPoint.Name;
        if (!_pointAxisKeys.TryGetValue(_selectedTeachingPoint.Id, out var keys) || keys.Count == 0)
        {
            StatusMessage = "该示教点没有关联的轴";
            return;
        }

        IsBusy = true;
        StatusMessage = "读取中...";
        try
        {
            foreach (var item in CurrentPositions)
            {
                if (item.Axis.IsBusAxis())
                    _ = ReadBusAxisAsync(item);
                else if (item.Axis.IsAkribisAxis())
                    ReadAkribisPosition(item);
            }
            IsModified = true;
            StatusMessage = "坐标读取完成";
        }
        catch (Exception ex)
        {
            StatusMessage = $"读取失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReadBusAxisAsync(AxisPositionItem item)
    {
        if (!_busAxisDevice.IsConnected) return;
        var busId = item.Axis.ToBusAxisId(SelectedStation);
        var result = await _busAxisDevice.GetPositionAsync(busId);
        if (result.IsSuccess)
            item.Position = result.Data;
    }

    private void ReadAkribisPosition(AxisPositionItem item)
    {
        var (instanceName, akAxis) = item.Axis.ToAkribis(SelectedStation);
        if (!_akribisInstances.TryGetValue(instanceName, out var motion)) return;

        item.Position = akAxis switch
        {
            AkribisAxisId.X => motion.PositionX,
            AkribisAxisId.Y => motion.PositionY,
            AkribisAxisId.Z => motion.PositionZ,
            _ => 0,
        };
    }

    // ========== 内部 ==========

    private void SaveCurrentPointData()
    {
        if (_selectedTeachingPoint == null) return;

        var positions = new Dictionary<EAxis, double>();
        foreach (var item in CurrentPositions)
            positions[item.Axis] = item.Position;
        _pointData[_selectedTeachingPoint.Id] = positions;
    }

    private void LoadPointData(TeachingPointItem? point)
    {
        CurrentPositions.Clear();
        if (point == null) return;

        var keys = _pointAxisKeys.TryGetValue(point.Id, out var axisKeys)
            ? axisKeys : [];
        // 按 keys 顺序构建（拖动排序后的顺序）
        var selectedAxes = keys
            .Select(k => AllAxes.FirstOrDefault(a => a.Axis == k))
            .Where(a => a != null)
            .Cast<AxisSelectionItem>()
            .ToList();

        if (_pointData.TryGetValue(point.Id, out var positions))
        {
            foreach (var axis in selectedAxes)
            {
                positions.TryGetValue(axis.Axis, out var pos);
                CurrentPositions.Add(new AxisPositionItem
                {
                    Axis = axis.Axis,
                    Position = pos,
                    OnChanged = () => IsModified = true,
                });
            }
        }
        else
        {
            foreach (var axis in selectedAxes)
                CurrentPositions.Add(new AxisPositionItem
                {
                    Axis = axis.Axis,
                    Position = 0,
                    OnChanged = () => IsModified = true,
                });
        }
    }

    // ========== 拖拽排序 ==========

    public void DragOver(IDropInfo dropInfo)
    {
        if (dropInfo.Data is TeachingPointItem tpi && tpi.Station == _selectedStation)
        {
            dropInfo.Effects = DragDropEffects.Move;
            dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
        }
        else if (dropInfo.Data is AxisPositionItem)
        {
            dropInfo.Effects = DragDropEffects.Move;
            dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
        }
    }

    public void Drop(IDropInfo dropInfo)
    {
        if (dropInfo.Data is TeachingPointItem dragged)
            DropTeachingPoint(dragged, dropInfo.InsertIndex);
        else if (dropInfo.Data is AxisPositionItem axisItem)
            DropAxisPosition(axisItem, dropInfo.InsertIndex);
    }

    private void DropTeachingPoint(TeachingPointItem dragged, int insertIndex)
    {
        var stationItems = AllPoints.Where(p => p.Station == _selectedStation).ToList();

        int targetAllIndex;
        if (insertIndex < stationItems.Count)
            targetAllIndex = AllPoints.IndexOf(stationItems[insertIndex]);
        else
            targetAllIndex = AllPoints.Count;

        var draggedIdx = AllPoints.IndexOf(dragged);
        if (draggedIdx < 0 || draggedIdx == targetAllIndex) return;

        if (draggedIdx < targetAllIndex)
            targetAllIndex--;

        AllPoints.Move(draggedIdx, targetAllIndex);
        NotifyOfPropertyChange(nameof(StationPoints));
        IsModified = true;
        StatusMessage = "示教点已重新排序";
    }

    private void DropAxisPosition(AxisPositionItem axisItem, int insertIndex)
    {
        var draggedIdx = CurrentPositions.IndexOf(axisItem);
        if (draggedIdx < 0 || draggedIdx == insertIndex) return;

        var targetIdx = insertIndex;
        if (draggedIdx < targetIdx)
            targetIdx--;

        if (draggedIdx == targetIdx) return;

        SaveCurrentPointData();
        CurrentPositions.Move(draggedIdx, targetIdx);
        IsModified = true;
        if (_selectedTeachingPoint != null)
        {
            _pointAxisKeys[_selectedTeachingPoint.Id] = CurrentPositions.Select(p => p.Axis).ToList();
        }
    }
}
