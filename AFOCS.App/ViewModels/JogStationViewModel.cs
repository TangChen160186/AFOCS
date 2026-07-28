using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Windows;
using AFOCS.App.Services;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;

namespace AFOCS.App.ViewModels;

// ====================================================================
// 接口
// ====================================================================

public interface IJogStation : ITool { }

// ====================================================================
// 工位筛选
// ====================================================================

public enum StationFilter
{
    全部工位,
    左工位,
    右工位,
}

// ====================================================================
// 数据项：总线轴 Jog 项
// ====================================================================

public class BusAxisJogItem : INotifyPropertyChanged
{
    private bool _subscribed;

    public AxisId AxisId { get; }
    public string Name { get; }
    public string GroupName { get; }

    private double _position;
    public double Position
    {
        get => _position;
        set { if (Math.Abs(_position - value) > 0.001) { _position = value; OnPropertyChanged(); } }
    }

    private bool _isAlarm;
    public bool IsAlarm
    {
        get => _isAlarm;
        set { if (_isAlarm != value) { _isAlarm = value; OnPropertyChanged(); } }
    }

    private bool _isPositiveLimit;
    public bool IsPositiveLimit
    {
        get => _isPositiveLimit;
        set { if (_isPositiveLimit != value) { _isPositiveLimit = value; OnPropertyChanged(); } }
    }

    private bool _isNegativeLimit;
    public bool IsNegativeLimit
    {
        get => _isNegativeLimit;
        set { if (_isNegativeLimit != value) { _isNegativeLimit = value; OnPropertyChanged(); } }
    }

    private bool _isEmergencyStop;
    public bool IsEmergencyStop
    {
        get => _isEmergencyStop;
        set { if (_isEmergencyStop != value) { _isEmergencyStop = value; OnPropertyChanged(); } }
    }

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); } }
    }

    public string StatusText
    {
        get
        {
            if (IsEmergencyStop) return "急停";
            if (IsAlarm) return "报警";
            if (IsEnabled) return "已使能";
            return "未使能";
        }
    }

    public BusAxisJogItem(AxisId axisId, string name, string groupName)
    {
        AxisId = axisId;
        Name = name;
        GroupName = groupName;
    }

    public void Subscribe(IBusAxisDevice device)
    {
        if (_subscribed) return;
        _subscribed = true;
        device.AxisStateChanged += OnAxisStateChanged;
    }

    public void Unsubscribe(IBusAxisDevice device)
    {
        if (!_subscribed) return;
        _subscribed = false;
        device.AxisStateChanged -= OnAxisStateChanged;
    }

    private void OnAxisStateChanged(object? sender, BusAxisStateChangedEventArgs e)
    {
        if (e.AxisId != AxisId) return;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            Position = e.Position;
            IsAlarm = e.IsAlarm;
            IsPositiveLimit = e.IsPositiveLimit;
            IsNegativeLimit = e.IsNegativeLimit;
            IsEmergencyStop = e.IsEmergencyStop;
            IsEnabled = e.IsEnabled;
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ====================================================================
// 数据项：雅克贝斯站 Jog 项
// ====================================================================

public class AkribisStationJogItem : INotifyPropertyChanged
{
    private readonly IAkribisMotion _motion;
    private bool _subscribed;

    public string Name { get; }
    public IAkribisMotion Motion => _motion;

    private int _posX;
    public int PosX
    {
        get => _posX;
        set { if (_posX != value) { _posX = value; OnPropertyChanged(); } }
    }

    private int _posY;
    public int PosY
    {
        get => _posY;
        set { if (_posY != value) { _posY = value; OnPropertyChanged(); } }
    }

    private int _posZ;
    public int PosZ
    {
        get => _posZ;
        set { if (_posZ != value) { _posZ = value; OnPropertyChanged(); } }
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set { if (_isConnected != value) { _isConnected = value; OnPropertyChanged(); } }
    }

    public AkribisStationJogItem(string name, IAkribisMotion motion)
    {
        Name = name;
        _motion = motion;
        _isConnected = motion.IsConnected;
    }

    public void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;
        _motion.PositionChanged += OnPositionChanged;
        PosX = _motion.PositionX;
        PosY = _motion.PositionY;
        PosZ = _motion.PositionZ;
        IsConnected = _motion.IsConnected;
    }

    public void Unsubscribe()
    {
        if (!_subscribed) return;
        _subscribed = false;
        _motion.PositionChanged -= OnPositionChanged;
    }

    private void OnPositionChanged(object? sender, AkribisPositionChangedEventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            PosX = e.X;
            PosY = e.Y;
            PosZ = e.Z;
            IsConnected = _motion.IsConnected;
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ====================================================================
// 数据项：夹爪 Jog 项
// ====================================================================

public class GripperJogItem : INotifyPropertyChanged
{
    private readonly ISmcGripper _gripper;
    private bool _subscribed;

    public string Name => _gripper.DisplayName;
    public ISmcGripper Gripper => _gripper;

    private int _position;
    public int Position
    {
        get => _position;
        set { if (_position != value) { _position = value; OnPropertyChanged(); } }
    }

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); } }
    }

    private bool _isAlarm;
    public bool IsAlarm
    {
        get => _isAlarm;
        set { if (_isAlarm != value) { _isAlarm = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); } }
    }

    public string StatusText
    {
        get
        {
            if (IsAlarm) return "报警";
            if (IsEnabled) return "已使能";
            return "未使能";
        }
    }

    public GripperJogItem(ISmcGripper gripper)
    {
        _gripper = gripper;
    }

    public void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;
        _gripper.DataChanged += OnDataChanged;
        _position = _gripper.CurrentPosition;
        _isEnabled = _gripper.IsEnabledCached;
        _isAlarm = _gripper.IsAlarmCached;
    }

    public void Unsubscribe()
    {
        if (!_subscribed) return;
        _subscribed = false;
        _gripper.DataChanged -= OnDataChanged;
    }

    private void OnDataChanged(object? sender, GripperDataChangedEventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            Position = e.CurrentPosition;
            IsEnabled = e.IsEnabled;
            IsAlarm = e.IsAlarm;
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ====================================================================
// JogStation ViewModel
// ====================================================================

[Export]
[Export(typeof(IJogStation))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class JogStationViewModel : Tool, IJogStation
{
    private readonly IBusAxisDevice _busAxisDevice;
    private readonly IToastService _toastService;

    public override PaneLocation PreferredLocation => PaneLocation.Left;
    public override double PreferredWidth => 400;
    public override double PreferredHeight => 600;

    public override string DisplayName => "轴手柄控制";

    // ========== 工位筛选 ==========

    public StationFilter[] StationOptions { get; } = [StationFilter.全部工位, StationFilter.左工位, StationFilter.右工位];

    private StationFilter _selectedStation = StationFilter.全部工位;
    public StationFilter SelectedStation
    {
        get => _selectedStation;
        set
        {
            if (_selectedStation == value) return;
            _selectedStation = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(FilteredBusAxisGroups));
            NotifyOfPropertyChange(nameof(FilteredAkribisStations));
            NotifyOfPropertyChange(nameof(FilteredGrippers));
        }
    }

    // ========== 总线轴数据 ==========

    public ObservableCollection<BusAxisJogItem> BusAxes { get; } = [];

    public IEnumerable<IGrouping<string, BusAxisJogItem>> FilteredBusAxisGroups
    {
        get
        {
            var filtered = _selectedStation switch
            {
                StationFilter.左工位 => BusAxes.Where(x => x.GroupName.Contains("左工位")),
                StationFilter.右工位 => BusAxes.Where(x => x.GroupName.Contains("右工位")),
                _ => BusAxes.AsEnumerable(),
            };
            return filtered.GroupBy(x => x.GroupName);
        }
    }

    // ========== 雅克贝斯数据 ==========

    public ObservableCollection<AkribisStationJogItem> AkribisStations { get; } = [];

    public IEnumerable<AkribisStationJogItem> FilteredAkribisStations
    {
        get
        {
            return _selectedStation switch
            {
                StationFilter.左工位 => AkribisStations.Where(x => x.Name.Contains("左工位")),
                StationFilter.右工位 => AkribisStations.Where(x => x.Name.Contains("右工位")),
                _ => AkribisStations,
            };
        }
    }

    // ========== 夹爪数据 ==========

    public ObservableCollection<GripperJogItem> Grippers { get; } = [];

    public IEnumerable<GripperJogItem> FilteredGrippers
    {
        get
        {
            return _selectedStation switch
            {
                StationFilter.左工位 => Grippers.Where(x => x.Name.Contains("左")),
                StationFilter.右工位 => Grippers.Where(x => x.Name.Contains("右")),
                _ => Grippers,
            };
        }
    }

    // ========== Jog 步长 ==========

    private double _busJogStepLarge = 10;
    public double BusJogStepLarge
    {
        get => _busJogStepLarge;
        set { _busJogStepLarge = value; NotifyOfPropertyChange(); }
    }

    private double _busJogStepSmall = 0.5;
    public double BusJogStepSmall
    {
        get => _busJogStepSmall;
        set { _busJogStepSmall = value; NotifyOfPropertyChange(); }
    }

    private int _akribisJogStepLarge = 1000;
    public int AkribisJogStepLarge
    {
        get => _akribisJogStepLarge;
        set { _akribisJogStepLarge = value; NotifyOfPropertyChange(); }
    }

    private int _akribisJogStepSmall = 100;
    public int AkribisJogStepSmall
    {
        get => _akribisJogStepSmall;
        set { _akribisJogStepSmall = value; NotifyOfPropertyChange(); }
    }

    private ushort _gripperSpeed = 50;
    public ushort GripperSpeed
    {
        get => _gripperSpeed;
        set { _gripperSpeed = value; NotifyOfPropertyChange(); }
    }

    private ushort _gripperTargetPosition = 200;
    public ushort GripperTargetPosition
    {
        get => _gripperTargetPosition;
        set { _gripperTargetPosition = value; NotifyOfPropertyChange(); }
    }

    // ========== 状态 ==========

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; NotifyOfPropertyChange(); }
    }

    [ImportingConstructor]
    public JogStationViewModel(
        IBusAxisDevice busAxisDevice,
        IToastService toastService,
        AkribisLeftCouplingL leftL,
        AkribisLeftCouplingR leftR,
        AkribisRightCouplingL rightL,
        AkribisRightCouplingR rightR,
        LeftCouplingLGripper gripperLL,
        LeftCouplingRGripper gripperLR,
        RightCouplingLGripper gripperRL,
        RightCouplingRGripper gripperRR)
    {
        _busAxisDevice = busAxisDevice;
        _toastService = toastService;

        BuildBusAxisItems();
        BuildAkribisItems(leftL, leftR, rightL, rightR);
        BuildGripperItems(gripperLL, gripperLR, gripperRL, gripperRR);
    }

    // ========== 构建数据 ==========

    private void BuildBusAxisItems()
    {
        AddBusAxis(AxisId.LeftCamUpX, "左工位相机");
        AddBusAxis(AxisId.LeftCamUpY, "左工位相机");
        AddBusAxis(AxisId.LeftCamUpZ, "左工位相机");
        AddBusAxis(AxisId.LeftCamSideY, "左工位相机");

        AddBusAxis(AxisId.LeftCouplingLThetaX, "左工位耦合");
        AddBusAxis(AxisId.LeftCouplingLThetaY, "左工位耦合");
        AddBusAxis(AxisId.LeftCouplingLThetaZ, "左工位耦合");
        AddBusAxis(AxisId.LeftCouplingRThetaX, "左工位耦合");
        AddBusAxis(AxisId.LeftCouplingRThetaY, "左工位耦合");
        AddBusAxis(AxisId.LeftCouplingRThetaZ, "左工位耦合");

        AddBusAxis(AxisId.RightCamUpX, "右工位相机");
        AddBusAxis(AxisId.RightCamUpY, "右工位相机");
        AddBusAxis(AxisId.RightCamUpZ, "右工位相机");
        AddBusAxis(AxisId.RightCamSideY, "右工位相机");

        AddBusAxis(AxisId.RightCouplingLThetaX, "右工位耦合");
        AddBusAxis(AxisId.RightCouplingLThetaY, "右工位耦合");
        AddBusAxis(AxisId.RightCouplingLThetaZ, "右工位耦合");
        AddBusAxis(AxisId.RightCouplingRThetaX, "右工位耦合");
        AddBusAxis(AxisId.RightCouplingRThetaY, "右工位耦合");
        AddBusAxis(AxisId.RightCouplingRThetaZ, "右工位耦合");
    }

    private void AddBusAxis(AxisId id, string group)
    {
        BusAxes.Add(new BusAxisJogItem(id, BusAxisDevice.GetAxisDisplayName(id), group));
    }

    private void BuildAkribisItems(
        AkribisLeftCouplingL leftL, AkribisLeftCouplingR leftR,
        AkribisRightCouplingL rightL, AkribisRightCouplingR rightR)
    {
        AkribisStations.Add(new AkribisStationJogItem("左工位左耦合", leftL));
        AkribisStations.Add(new AkribisStationJogItem("左工位右耦合", leftR));
        AkribisStations.Add(new AkribisStationJogItem("右工位左耦合", rightL));
        AkribisStations.Add(new AkribisStationJogItem("右工位右耦合", rightR));
    }

    private void BuildGripperItems(
        LeftCouplingLGripper ll, LeftCouplingRGripper lr,
        RightCouplingLGripper rl, RightCouplingRGripper rr)
    {
        Grippers.Add(new GripperJogItem(ll));
        Grippers.Add(new GripperJogItem(lr));
        Grippers.Add(new GripperJogItem(rl));
        Grippers.Add(new GripperJogItem(rr));
    }

    // ========== 生命周期 ==========

    protected override void OnViewAttached(object view, object context)
    {
        base.OnViewAttached(view, context);

        foreach (var item in BusAxes) item.Subscribe(_busAxisDevice);
        foreach (var item in AkribisStations) item.Subscribe();
        foreach (var item in Grippers) item.Subscribe();

        if (view is FrameworkElement fe)
            fe.Unloaded += OnViewUnloaded;
    }

    private void OnViewUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
            fe.Unloaded -= OnViewUnloaded;
        foreach (var item in BusAxes) item.Unsubscribe(_busAxisDevice);
        foreach (var item in AkribisStations) item.Unsubscribe();
        foreach (var item in Grippers) item.Unsubscribe();
    }

    // ========== 总线轴 Jog ==========

    public async Task BusJogPlusLargeAsync(BusAxisJogItem item) => await BusJogMoveAsync(item, BusJogStepLarge);
    public async Task BusJogMinusLargeAsync(BusAxisJogItem item) => await BusJogMoveAsync(item, -BusJogStepLarge);
    public async Task BusJogPlusSmallAsync(BusAxisJogItem item) => await BusJogMoveAsync(item, BusJogStepSmall);
    public async Task BusJogMinusSmallAsync(BusAxisJogItem item) => await BusJogMoveAsync(item, -BusJogStepSmall);

    private async Task BusJogMoveAsync(BusAxisJogItem item, double distance)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var r = await _busAxisDevice.MovePmoveAsync(item.AxisId, distance);
            if (!r.IsSuccess)
                _toastService.ShowError(r.Message);
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"{item.Name} 移动异常: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    public async Task BusStopAsync(BusAxisJogItem item)
    {
        var r = await _busAxisDevice.StopAxisAsync(item.AxisId);
        if (!r.IsSuccess)
            _toastService.ShowError(r.Message);
    }

    public async Task BusHomeAsync(BusAxisJogItem item)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var r = await _busAxisDevice.MoveHomeAsync(item.AxisId);
            if (!r.IsSuccess)
                _toastService.ShowError($"{item.Name} 回零失败: {r.Message}");
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"{item.Name} 回零异常: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    // ========== 雅克贝斯 Jog ==========

    public async Task AkribisJogPlusLargeAsync(AkribisStationJogItem station, string axisName)
        => await AkribisJogMoveAsync(station, ParseAxis(axisName), AkribisJogStepLarge);
    public async Task AkribisJogMinusLargeAsync(AkribisStationJogItem station, string axisName)
        => await AkribisJogMoveAsync(station, ParseAxis(axisName), -AkribisJogStepLarge);
    public async Task AkribisJogPlusSmallAsync(AkribisStationJogItem station, string axisName)
        => await AkribisJogMoveAsync(station, ParseAxis(axisName), AkribisJogStepSmall);
    public async Task AkribisJogMinusSmallAsync(AkribisStationJogItem station, string axisName)
        => await AkribisJogMoveAsync(station, ParseAxis(axisName), -AkribisJogStepSmall);

    private static AkribisAxisId ParseAxis(string name) => name switch
    {
        "X" => AkribisAxisId.X,
        "Y" => AkribisAxisId.Y,
        "Z" => AkribisAxisId.Z,
        _ => throw new ArgumentException($"未知轴: {name}")
    };

    private async Task AkribisJogMoveAsync(AkribisStationJogItem station, AkribisAxisId axis, int distance)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var r = await station.Motion.MoveRelativeAsync(axis, distance);
            if (!r.IsSuccess)
                _toastService.ShowError($"{station.Name} {axis}: {r.Message}");
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"{station.Name} {axis} 移动异常: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    public async Task AkribisStopAsync(AkribisStationJogItem station)
    {
        var r = await station.Motion.StopAxisAsync();
        if (!r.IsSuccess)
            _toastService.ShowError($"{station.Name}: {r.Message}");
    }

    public async Task AkribisHomeAsync(AkribisStationJogItem station, string axisName)
    {
        if (IsBusy) return;
        var axis = ParseAxis(axisName);
        IsBusy = true;
        try
        {
            var r = await station.Motion.HomeAsync(axis);
            if (!r.IsSuccess)
                _toastService.ShowError($"{station.Name} {axis} 回零失败: {r.Message}");
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"{station.Name} {axis} 回零异常: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    // ========== 夹爪控制 ==========

    public async Task GripperHomeAsync(GripperJogItem item)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var r = await item.Gripper.HomeAsync();
            if (!r.IsSuccess)
                _toastService.ShowError($"{item.Name} 回零失败: {r.Message}");
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"{item.Name} 回零异常: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    public async Task GripperEnableAsync(GripperJogItem item)
    {
        var r = await item.Gripper.EnableAsync();
        if (!r.IsSuccess)
            _toastService.ShowError($"{item.Name} 使能失败: {r.Message}");
    }

    public async Task GripperAlarmResetAsync(GripperJogItem item)
    {
        var r = await item.Gripper.AlarmResetAsync();
        if (!r.IsSuccess)
            _toastService.ShowError($"{item.Name} 报警复位失败: {r.Message}");
    }

    public async Task GripperMoveAsync(GripperJogItem item)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var r = await item.Gripper.MoveAsync(GripperSpeed, GripperTargetPosition);
            if (!r.IsSuccess)
                _toastService.ShowError($"{item.Name} 动作失败: {r.Message}");
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"{item.Name} 动作异常: {ex.Message}");
        }
        finally { IsBusy = false; }
    }
}
