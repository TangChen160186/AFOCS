using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Framework;
using AFOCS.Infrastructure;

namespace AFOCS.App.ViewModels
{
    // ========== JSON 持久化 POCO ==========

    public class TeachingPointsConfig
    {
        public List<string> SelectedAxisKeys { get; set; } = [];
        public List<TeachingPointPoco> Points { get; set; } = [];
    }

    public class TeachingPointPoco
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, double> AxisPositions { get; set; } = [];
    }

    // ========== UI 绑定辅助类型 ==========

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

    public class TeachingPointItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

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

    // ========== Document ViewModel ==========

    public class TeachingPointsDocumentViewModel : Document
    {
        private readonly IConfigService _configService;

        // ========== 轴选择 ==========
        public ObservableCollection<AxisSelectionItem> AvailableAxes { get; } = [];

        // ========== 示教点列表 ==========
        public ObservableCollection<TeachingPointItem> TeachingPoints { get; } = [];

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
                LoadPointData(value);
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

        // 内存中的示教点数据
        private readonly Dictionary<string, Dictionary<string, double>> _pointData = [];

        public TeachingPointsDocumentViewModel(IConfigService configService)
        {
            _configService = configService;
            DisplayName = "示教点";

            InitializeAvailableAxes();
            _ = LoadAsync();
        }

        // ========== 初始化 ==========

        private void InitializeAvailableAxes()
        {
            foreach (AxisId id in Enum.GetValues<AxisId>())
            {
                AvailableAxes.Add(new AxisSelectionItem
                {
                    Key = AxisKey(id),
                    DisplayName = BusAxisDevice.GetAxisDisplayName(id),
                });
            }

            foreach (LinearAxisId id in Enum.GetValues<LinearAxisId>())
            {
                AvailableAxes.Add(new AxisSelectionItem
                {
                    Key = AxisKey(id),
                    DisplayName = GetLinearAxisDisplayName(id),
                });
            }
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
                    StatusMessage = "就绪";
                    return;
                }

                foreach (var axis in AvailableAxes)
                    axis.IsSelected = config.SelectedAxisKeys.Contains(axis.Key);

                _pointData.Clear();
                TeachingPoints.Clear();
                foreach (var point in config.Points)
                {
                    _pointData[point.Name] = new Dictionary<string, double>(point.AxisPositions);
                    TeachingPoints.Add(new TeachingPointItem { Name = point.Name });
                }

                if (TeachingPoints.Count > 0)
                    SelectedTeachingPoint = TeachingPoints[0];

                StatusMessage = $"已加载 {TeachingPoints.Count} 个示教点";
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
                    SelectedAxisKeys = AvailableAxes.Where(a => a.IsSelected).Select(a => a.Key).ToList(),
                    Points = _pointData.Select(kvp => new TeachingPointPoco
                    {
                        Name = kvp.Key,
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
            var selectedKeys = AvailableAxes.Where(a => a.IsSelected).ToList();
            if (selectedKeys.Count == 0)
            {
                StatusMessage = "请先选择至少一个轴";
                return;
            }

            var name = $"示教点_{TeachingPoints.Count + 1}";
            var idx = 1;
            while (_pointData.ContainsKey(name))
            {
                idx++;
                name = $"示教点_{TeachingPoints.Count + idx}";
            }

            var item = new TeachingPointItem { Name = name };
            TeachingPoints.Add(item);

            var positions = new Dictionary<string, double>();
            foreach (var axis in selectedKeys)
                positions[axis.Key] = 0;
            _pointData[name] = positions;

            SelectedTeachingPoint = item;
            IsModified = true;
            StatusMessage = $"已添加: {name}";
        }

        public void DeleteTeachingPoint()
        {
            if (SelectedTeachingPoint == null) return;

            var name = SelectedTeachingPoint.Name;
            _pointData.Remove(name);
            TeachingPoints.Remove(SelectedTeachingPoint);
            SelectedTeachingPoint = TeachingPoints.FirstOrDefault();
            IsModified = true;
            StatusMessage = $"已删除: {name}";
        }

        // ========== 轴选择应用 ==========

        public void ApplyAxisSelection()
        {
            SaveCurrentPointData();

            var selectedKeys = AvailableAxes.Where(a => a.IsSelected).Select(a => a.Key).ToHashSet();

            foreach (var kvp in _pointData)
            {
                var positions = kvp.Value;
                var toRemove = positions.Keys.Where(k => !selectedKeys.Contains(k)).ToList();
                foreach (var key in toRemove)
                    positions.Remove(key);
                foreach (var key in selectedKeys)
                {
                    if (!positions.ContainsKey(key))
                        positions[key] = 0;
                }
            }

            if (SelectedTeachingPoint != null)
                LoadPointData(SelectedTeachingPoint);

            IsModified = true;
        }

        // ========== 读取当前坐标（预留接口） ==========

        public void ReadCurrentPosition()
        {
            StatusMessage = "读取当前位置需要设备支持，暂未实现";
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

            var axisToDisplay = AvailableAxes.Where(a => a.IsSelected).ToList();

            if (_pointData.TryGetValue(point.Name, out var positions))
            {
                foreach (var axis in axisToDisplay)
                {
                    positions.TryGetValue(axis.Key, out var pos);
                    CurrentPositions.Add(MakePositionItem(axis.Key, axis.DisplayName, pos));
                }
            }
            else
            {
                foreach (var axis in axisToDisplay)
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
        public static string AxisKey(LinearAxisId id) => $"LinearAxis_{(int)id}";

        private static string GetLinearAxisDisplayName(LinearAxisId id)
        {
            return id switch
            {
                LinearAxisId.LeftCouplingLX => "左耦合左X轴(直线)",
                LinearAxisId.LeftCouplingLY => "左耦合左Y轴(直线)",
                LinearAxisId.LeftCouplingLZ => "左耦合左Z轴(直线)",
                LinearAxisId.LeftCouplingRX => "左耦合右X轴(直线)",
                LinearAxisId.LeftCouplingRY => "左耦合右Y轴(直线)",
                LinearAxisId.LeftCouplingRZ => "左耦合右Z轴(直线)",
                LinearAxisId.RightCouplingLX => "右耦合左X轴(直线)",
                LinearAxisId.RightCouplingLY => "右耦合左Y轴(直线)",
                LinearAxisId.RightCouplingLZ => "右耦合左Z轴(直线)",
                LinearAxisId.RightCouplingRX => "右耦合右X轴(直线)",
                LinearAxisId.RightCouplingRY => "右耦合右Y轴(直线)",
                LinearAxisId.RightCouplingRZ => "右耦合右Z轴(直线)",
                _ => id.ToString(),
            };
        }
    }
}
