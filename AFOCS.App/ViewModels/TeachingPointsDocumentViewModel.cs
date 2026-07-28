using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Framework;
using AFOCS.Infrastructure;

namespace AFOCS.App.ViewModels;

// ==================== JSON 持久化 POCO ====================

public class TeachingPointsConfig
{
    public List<TeachingPointPoco> Points { get; set; } = [];
}

public class TeachingPointPoco
{
    public string Name { get; set; } = string.Empty;
    public string Station { get; set; } = string.Empty; // "左工位" / "右工位"
    public List<string> AxisKeys { get; set; } = []; // 该点包含的轴
    public Dictionary<string, double> AxisPositions { get; set; } = [];
}

// ==================== UI 绑定辅助类型 ====================

public class AxisSelectionItem : INotifyPropertyChanged
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class AxisGroupItem
{
    public string GroupName { get; set; } = string.Empty;
    public ObservableCollection<AxisSelectionItem> Axes { get; set; } = [];
}

public class TeachingPointItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Station { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class AxisPositionItem : INotifyPropertyChanged
{
    public Action? OnChanged { get; set; }

    public string AxisKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    private double _position;
    public double Position
    {
        get => _position;
        set
        {
            if (Math.Abs(_position - value) < 0.0001) return;
            _position = value;
            OnPropertyChanged();
            OnChanged?.Invoke();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ==================== Document ViewModel ====================

[Export]
public class TeachingPointsDocumentViewModel : Document
{
    private readonly IConfigService _configService;

    // ========== 工位 ==========

    public string[] Stations { get; } = ["左工位", "右工位"];

    private string _selectedStation = "左工位";
    public string SelectedStation
    {
        get => _selectedStation;
        set
        {
            if (_selectedStation == value) return;
            _selectedStation = value;
            NotifyOfPropertyChange();
            RefreshStationGroups();
            NotifyOfPropertyChange(nameof(StationAxisGroups));
            NotifyOfPropertyChange(nameof(StationPoints));
            SelectedTeachingPoint = null;
        }
    }

    public void SelectStation(string station) => SelectedStation = station;

    // ========== 全量轴列表（存储用） ==========

    public ObservableCollection<AxisSelectionItem> AllAxes { get; } = [];

    /// <summary>当前工位的轴分组（相机轴 / 耦合轴）</summary>
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
        }
    }

    public bool IsPointSelected => _selectedTeachingPoint != null;

    // 当前编辑的名字
    public string PointName
    {
        get => _selectedTeachingPoint?.Name ?? string.Empty;
        set
        {
            if (_selectedTeachingPoint == null || _selectedTeachingPoint.Name == value) return;
            var oldName = _selectedTeachingPoint.Name;
            _selectedTeachingPoint.Name = value;
            if (_pointData.Remove(oldName, out var data))
                _pointData[value] = data;
            if (_pointAxisKeys.Remove(oldName, out var keys))
                _pointAxisKeys[value] = keys;
            NotifyOfPropertyChange();
            IsModified = true;
        }
    }

    // ========== 当前编辑点的轴位置 ==========

    public ObservableCollection<AxisPositionItem> CurrentPositions { get; } = [];

    private bool _isModified;
    public bool IsModified
    {
        get => _isModified;
        set { _isModified = value; NotifyOfPropertyChange(); }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; NotifyOfPropertyChange(); }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; NotifyOfPropertyChange(); }
    }

    // 内存中的数据：pointName -> (axisKey -> position)
    private readonly Dictionary<string, Dictionary<string, double>> _pointData = [];
    // 每个点独立的轴键列表
    private readonly Dictionary<string, List<string>> _pointAxisKeys = [];

    [ImportingConstructor]
    public TeachingPointsDocumentViewModel(IConfigService configService)
    {
        _configService = configService;
        DisplayName = "示教点";

        InitializeAxes();
        _ = LoadAsync();
    }

    // ========== 初始化 ==========

    private void InitializeAxes()
    {
        // 按顺序添加所有轴：左工位 → 右工位（相机 → 耦合）
        AddBusAxes(Array.FindAll((AxisId[])Enum.GetValues<AxisId>(),
            id => id.ToString().StartsWith("Left")),
            "左工位", "相机轴", "耦合轴");
        AddBusAxes(Array.FindAll((AxisId[])Enum.GetValues<AxisId>(),
            id => id.ToString().StartsWith("Right")),
            "右工位", "相机轴", "耦合轴");
    }

    private void AddBusAxes(AxisId[] ids, string station, string camGroup, string couplingGroup)
    {
        foreach (var id in ids)
        {
            var displayName = BusAxisDevice.GetAxisDisplayName(id);
            var groupName = displayName.Contains("相机") ? camGroup : couplingGroup;
            AllAxes.Add(new AxisSelectionItem
            {
                Key = AxisKey(id),
                DisplayName = $"{displayName}",
                // 默认选中：内调芯 θX/θY/θZ 都选
                IsSelected = displayName.Contains("θ")
            });
        }
    }

    private void RefreshStationGroups()
    {
        StationAxisGroups.Clear();
        // 分类：当前工位的相机轴和耦合轴
        var prefix = SelectedStation == "左工位" ? "左" : "右";
        var camGroup = "相机轴";
        var couplingGroup = "耦合轴";

        var camAxes = AllAxes.Where(a => a.DisplayName.StartsWith(prefix) && a.DisplayName.Contains("相机")).ToList();
        var couplingAxes = AllAxes.Where(a => a.DisplayName.StartsWith(prefix) && a.DisplayName.Contains("耦合")).ToList();

        if (camAxes.Count > 0)
            StationAxisGroups.Add(new AxisGroupItem { GroupName = camGroup, Axes = new ObservableCollection<AxisSelectionItem>(camAxes) });
        if (couplingAxes.Count > 0)
            StationAxisGroups.Add(new AxisGroupItem { GroupName = couplingGroup, Axes = new ObservableCollection<AxisSelectionItem>(couplingAxes) });
    }

    // ========== 加载 ==========

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "加载中...";
        try
        {
            var config = await _configService.LoadAsync<TeachingPointsConfig>();
            if (config == null)
            {
                RefreshStationGroups();
                StatusMessage = "就绪";
                return;
            }

            _pointData.Clear();
            _pointAxisKeys.Clear();
            AllPoints.Clear();
            foreach (var point in config.Points)
            {
                _pointData[point.Name] = new Dictionary<string, double>(point.AxisPositions);
                _pointAxisKeys[point.Name] = point.AxisKeys.Count > 0
                    ? new List<string>(point.AxisKeys)
                    : point.AxisPositions.Keys.ToList(); // 兼容旧数据
                AllPoints.Add(new TeachingPointItem { Name = point.Name, Station = point.Station });
            }

            RefreshStationGroups();
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

    // ========== 保存 ==========

    public async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = "保存中...";
        try
        {
            SaveCurrentPointData();

            var config = new TeachingPointsConfig
            {
                Points = _pointData.Select(kvp => new TeachingPointPoco
                {
                    Name = kvp.Key,
                    Station = AllPoints.FirstOrDefault(p => p.Name == kvp.Key)?.Station ?? "",
                    AxisKeys = _pointAxisKeys.TryGetValue(kvp.Key, out var keys) ? keys : [],
                    AxisPositions = kvp.Value,
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
        var selectedKeys = AllAxes.Where(a => a.IsSelected).ToList();
        if (selectedKeys.Count == 0)
        {
            StatusMessage = "请至少勾选一个轴";
            return;
        }

        // 生成唯一名称
        var baseName = $"{SelectedStation}示教点";
        var name = baseName;
        var idx = 1;
        while (_pointData.ContainsKey(name))
            name = $"{baseName}_{++idx}";

        var item = new TeachingPointItem { Name = name, Station = SelectedStation };
        AllPoints.Add(item);

        // 记录当前勾选的轴作为该点专属轴列表
        var keys = selectedKeys.Select(a => a.Key).ToList();
        _pointAxisKeys[name] = keys;

        var positions = new Dictionary<string, double>();
        foreach (var key in keys)
            positions[key] = 0;
        _pointData[name] = positions;

        NotifyOfPropertyChange(nameof(StationPoints));
        SelectedTeachingPoint = item;
        IsModified = true;
        StatusMessage = $"已添加: {name}";
    }

    public void DeleteTeachingPoint()
    {
        if (_selectedTeachingPoint == null) return;

        var name = _selectedTeachingPoint.Name;
        _pointData.Remove(name);
        _pointAxisKeys.Remove(name);
        AllPoints.Remove(_selectedTeachingPoint);
        SelectedTeachingPoint = null;
        NotifyOfPropertyChange(nameof(StationPoints));
        IsModified = true;
        StatusMessage = $"已删除: {name}";
    }

    // ========== 读取当前坐标 ==========

    public void ReadCurrentPosition()
    {
        if (CurrentPositions.Count == 0)
        {
            StatusMessage = "没有可读取的轴";
            return;
        }
        StatusMessage = "需要设备支持，暂未实现位置读取";
    }

    // ========== 内部 ==========

    private void SaveCurrentPointData()
    {
        if (_selectedTeachingPoint == null) return;
        var name = _selectedTeachingPoint.Name;
        if (string.IsNullOrWhiteSpace(name)) return;

        var positions = new Dictionary<string, double>();
        foreach (var item in CurrentPositions)
            positions[item.AxisKey] = item.Position;
        _pointData[name] = positions;
    }

    private void LoadPointData(TeachingPointItem? point)
    {
        CurrentPositions.Clear();
        if (point == null) return;

        // 用该点自己的轴键列表，不从全局 IsSelected 读取
        var keys = _pointAxisKeys.TryGetValue(point.Name, out var axisKeys)
            ? axisKeys : [];
        var selectedAxes = AllAxes.Where(a => keys.Contains(a.Key)).ToList();

        if (_pointData.TryGetValue(point.Name, out var positions))
        {
            foreach (var axis in selectedAxes)
            {
                positions.TryGetValue(axis.Key, out var pos);
                CurrentPositions.Add(MakePositionItem(axis.Key, axis.DisplayName, pos));
            }
        }
        else
        {
            foreach (var axis in selectedAxes)
                CurrentPositions.Add(MakePositionItem(axis.Key, axis.DisplayName, 0));
        }
    }

    private AxisPositionItem MakePositionItem(string key, string displayName, double position)
    {
        return new AxisPositionItem
        {
            AxisKey = key,
            DisplayName = displayName,
            Position = position,
            OnChanged = () => { IsModified = true; },
        };
    }

    // ========== 静态辅助 ==========

    public static string AxisKey(AxisId id) => $"BusAxis_{(int)id}";
}
