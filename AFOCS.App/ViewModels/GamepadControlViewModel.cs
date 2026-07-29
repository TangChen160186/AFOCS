using System.ComponentModel.Composition;
using AFOCS.App.Models;
using AFOCS.App.Services;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using AFOCS.Infrastructure;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels;

public interface IGamepadControl : ITool;

[Export]
[Export(typeof(IGamepadControl))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class GamepadControlViewModel(
    IBusAxisDevice busAxisDevice,IToastService toastService,
    [ImportMany] IEnumerable<IAkribisMotion> akribisMotions,
    [ImportMany] IEnumerable<ISmcGripper> grippers) : Tool, IGamepadControl
{
    private readonly IBusAxisDevice _busAxisDevice = busAxisDevice;
    private readonly IToastService _toastService = toastService;
    private readonly Dictionary<string, IAkribisMotion> _akribisInstances = [];
    private readonly Dictionary<string, ISmcGripper> _grippers = [];

    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 390;
    public override double PreferredHeight => 600;

    public override string DisplayName => "手柄控制";

    // ========== 工位 ==========

    private WorkPos _selectedStation = WorkPos.Left;
    public WorkPos SelectedStation
    {
        get => _selectedStation;
        set
        {
            if (Set(ref _selectedStation, value))
            {
                NotifyOfPropertyChange(nameof(AkribisLName));
                NotifyOfPropertyChange(nameof(AkribisRName));
                NotifyOfPropertyChange(nameof(AkribisLDisplay));
                NotifyOfPropertyChange(nameof(AkribisRDisplay));
                NotifyOfPropertyChange(nameof(GripperLName));
                NotifyOfPropertyChange(nameof(GripperRName));
                NotifyOfPropertyChange(nameof(GripperLDisplay));
                NotifyOfPropertyChange(nameof(GripperRDisplay));
                NotifyOfPropertyChange(nameof(HasGripperL));
                NotifyOfPropertyChange(nameof(HasGripperR));
                ReadAkribisPositions();
                ReadGripperPositions();
                RefreshAllDisplay();
            }
        }
    }

    // ========== 步长 ==========

    public int BusJogStep
    {
        get;
        set => Set(ref field, value);
    } = 1000;

    public int AkribisJogStep
    {
        get;
        set => Set(ref field, value);
    } = 100;


    // ========== Akribis 实例信息 ==========

    public string AkribisLName =>
        SelectedStation == WorkPos.Left ? nameof(AkribisLeftCouplingL) : nameof(AkribisRightCouplingL);

    public string AkribisRName =>
        SelectedStation == WorkPos.Left ? nameof(AkribisLeftCouplingR) : nameof(AkribisRightCouplingR);

    public string AkribisLDisplay =>
        SelectedStation == WorkPos.Left ? "左工位 - L耦合" : "右工位 - L耦合";

    public string AkribisRDisplay =>
        SelectedStation == WorkPos.Left ? "左工位 - R耦合" : "右工位 - R耦合";

    // ========== 夹爪信息 ==========

    public int GripperStep
    {
        get;
        set => Set(ref field, value);
    } = 50;

    public string GripperLName =>
        SelectedStation == WorkPos.Left ? nameof(LeftCouplingLGripper) : nameof(RightCouplingLGripper);

    public string GripperRName =>
        SelectedStation == WorkPos.Left ? nameof(LeftCouplingRGripper) : nameof(RightCouplingRGripper);

    public string GripperLDisplay =>
        SelectedStation == WorkPos.Left ? "左耦合左夹爪" : "右耦合左夹爪";

    public string GripperRDisplay =>
        SelectedStation == WorkPos.Left ? "左耦合右夹爪" : "右耦合右夹爪";

    // ========== 位置显示字段 ==========

    private double _camX, _camY, _camZ, _camSide;
    public string CameraPosText => $"X:{_camX:F1}  Y:{_camY:F1}  Z:{_camZ:F1}";
    public string CameraSidePosText => $"侧Y: {_camSide:F1}";
    public bool HasCameraPos => !double.IsNaN(_camX);

    private double _lrx, _lry, _lrz;
    public string LeftRotPosText => $"θX:{_lrx:F1}  θY:{_lry:F1}  θZ:{_lrz:F1}";
    public bool HasLeftRotPos => !double.IsNaN(_lrx);

    private double _rrx, _rry, _rrz;
    public string RightRotPosText => $"θX:{_rrx:F1}  θY:{_rry:F1}  θZ:{_rrz:F1}";
    public bool HasRightRotPos => !double.IsNaN(_rrx);

    private int _alx, _aly, _alz;
    public string AkribisLPosText => $"X:{_alx}  Y:{_aly}  Z:{_alz}";
    public bool HasAkribisLPos => _akribisInstances.ContainsKey(AkribisLName);

    private int _arx, _ary, _arz;
    public string AkribisRPosText => $"X:{_arx}  Y:{_ary}  Z:{_arz}";
    public bool HasAkribisRPos => _akribisInstances.ContainsKey(AkribisRName);

    private int _glpos, _grpos;
    public string GripperLPosText => $"位置: {_glpos} / 400";
    public string GripperRPosText => $"位置: {_grpos} / 400";
    public bool HasGripperL => _grippers.ContainsKey(GripperLName);
    public bool HasGripperR => _grippers.ContainsKey(GripperRName);

    // ========== 状态 ==========

    public string StatusText
    {
        get;
        set => Set(ref field, value);
    } = "就绪";

    public bool IsBusy
    {
        get;
        set => Set(ref field, value);
    }

    // ========== 构造 & 事件订阅 ==========

    protected override Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        foreach (var motion in akribisMotions)
        {
            var name = motion.GetType().Name;
            _akribisInstances[name] = motion;
            motion.PositionChanged += OnAkribisPositionChanged;
        }

        foreach (var gripper in grippers)
        {
            var name = gripper.GetType().Name;
            _grippers[name] = gripper;
            gripper.DataChanged += OnGripperDataChanged;
        }

        _busAxisDevice.AxisStateChanged += OnBusAxisStateChanged;
        return base.OnInitializedAsync(cancellationToken);
    }


    // ========== 总线轴位置事件 ==========

    private void OnBusAxisStateChanged(object? sender, BusAxisStateChangedEventArgs e)
    {
        var (axis, station) = FromBusAxisId(e.BusAxisId);

        // 只处理匹配当前工位的事件
        if (station != SelectedStation) return;

        switch (axis)
        {
            case EAxis.CamUpX: _camX = e.Position; break;
            case EAxis.CamUpY: _camY = e.Position; break;
            case EAxis.CamUpZ: _camZ = e.Position; break;
            case EAxis.CamSideY: _camSide = e.Position; break;
            case EAxis.CouplingLThetaX: _lrx = e.Position; break;
            case EAxis.CouplingLThetaY: _lry = e.Position; break;
            case EAxis.CouplingLThetaZ: _lrz = e.Position; break;
            case EAxis.CouplingRThetaX: _rrx = e.Position; break;
            case EAxis.CouplingRThetaY: _rry = e.Position; break;
            case EAxis.CouplingRThetaZ: _rrz = e.Position; break;
            default: return;
        }

        Execute.OnUIThreadAsync(() => RefreshAllDisplay());
    }

    /// <summary>从 BusAxisId 反向解析为 (EAxis, 工位)</summary>
    private static (EAxis Axis, WorkPos Station) FromBusAxisId(BusAxisId busAxisId)
    {
        int val = (int)busAxisId;
        var station = val >= 10 ? WorkPos.Right : WorkPos.Left;
        int axisVal = val >= 10 ? val - 10 : val;
        return ((EAxis)axisVal, station);
    }

    // ========== 雅克贝斯位置事件 ==========

    private void OnAkribisPositionChanged(object? sender, AkribisPositionChangedEventArgs e)
    {
        if (sender is not IAkribisMotion motion) return;
        var name = motion.GetType().Name;

        if (name == AkribisLName)
        {
            _alx = e.X;
            _aly = e.Y;
            _alz = e.Z;
        }
        else if (name == AkribisRName)
        {
            _arx = e.X;
            _ary = e.Y;
            _arz = e.Z;
        }
        else return;

        Execute.OnUIThreadAsync(() => RefreshAllDisplay());
    }

    private void ReadAkribisPositions()
    {
        if (_akribisInstances.TryGetValue(AkribisLName, out var mL))
        {
            _alx = mL.PositionX;
            _aly = mL.PositionY;
            _alz = mL.PositionZ;
        }
        if (_akribisInstances.TryGetValue(AkribisRName, out var mR))
        {
            _arx = mR.PositionX;
            _ary = mR.PositionY;
            _arz = mR.PositionZ;
        }
    }

    // ========== 夹爪位置事件 ==========

    private void OnGripperDataChanged(object? sender, GripperDataChangedEventArgs e)
    {
        if (sender is not ISmcGripper gripper) return;
        var name = gripper.GetType().Name;

        if (name == GripperLName)
            _glpos = e.CurrentPosition;
        else if (name == GripperRName)
            _grpos = e.CurrentPosition;
        else return;

        Execute.OnUIThreadAsync(() => RefreshAllDisplay());
    }

    private void ReadGripperPositions()
    {
        if (_grippers.TryGetValue(GripperLName, out var gL))
            _glpos = gL.CurrentPosition;
        if (_grippers.TryGetValue(GripperRName, out var gR))
            _grpos = gR.CurrentPosition;
    }

    private Task RefreshAllDisplay()
    {
        NotifyOfPropertyChange(nameof(CameraPosText));
        NotifyOfPropertyChange(nameof(CameraSidePosText));
        NotifyOfPropertyChange(nameof(HasCameraPos));
        NotifyOfPropertyChange(nameof(LeftRotPosText));
        NotifyOfPropertyChange(nameof(HasLeftRotPos));
        NotifyOfPropertyChange(nameof(RightRotPosText));
        NotifyOfPropertyChange(nameof(HasRightRotPos));
        NotifyOfPropertyChange(nameof(AkribisLPosText));
        NotifyOfPropertyChange(nameof(HasAkribisLPos));
        NotifyOfPropertyChange(nameof(AkribisRPosText));
        NotifyOfPropertyChange(nameof(HasAkribisRPos));
        NotifyOfPropertyChange(nameof(GripperLPosText));
        NotifyOfPropertyChange(nameof(GripperRPosText));

        return Task.CompletedTask;
    }

    // ========== 总线轴 D-Pad ==========

    public Task JogBusCamera(string axisId, int direction) => JogBus(axisId switch
    {
        "X" => "CamUpX", "Y" => "CamUpY", "Z" => "CamUpZ", _ => axisId,
    }, direction);

    public Task JogBusCameraSide(int direction) => JogBus("CamSideY", direction);

    public Task JogBusLeftRot(string axisId, int direction) => JogBus(axisId switch
    {
        "X" => "CouplingLThetaX", "Y" => "CouplingLThetaY", "Z" => "CouplingLThetaZ", _ => axisId,
    }, direction);

    public Task JogBusRightRot(string axisId, int direction) => JogBus(axisId switch
    {
        "X" => "CouplingRThetaX", "Y" => "CouplingRThetaY", "Z" => "CouplingRThetaZ", _ => axisId,
    }, direction);

    private async Task JogBus(string axisName, int direction)
    {
        if (!Enum.TryParse<EAxis>(axisName, out var axis)) return;

        var distance = BusJogStep * direction;
        var busId = axis.ToBusAxisId(SelectedStation);

        IsBusy = true;
        StatusText = $"{axisName} {(direction > 0 ? "+" : "-")}...";
        NotifyOfPropertyChange(nameof(StatusText));
        NotifyOfPropertyChange(nameof(IsBusy));

        var result = await _busAxisDevice.MovePmoveAsync(busId, distance, posiMode: 0);

        IsBusy = false;
        if (result.IsSuccess)
            StatusText = "就绪";
        else
            _toastService.ShowWarning($"{axisName} 运动失败:\n{result.Message}");
        NotifyOfPropertyChange(nameof(StatusText));
        NotifyOfPropertyChange(nameof(IsBusy));
    }

    // ========== 雅克贝斯 D-Pad ==========

    public Task JogAkribisL(string axisId, int direction) => JogAkribis(AkribisLName, axisId, direction);
    public Task JogAkribisR(string axisId, int direction) => JogAkribis(AkribisRName, axisId, direction);

    private async Task JogAkribis(string instanceName, string axisId, int direction)
    {
        if (!Enum.TryParse<AkribisAxisId>(axisId, out var akAxis)) return;

        if (!_akribisInstances.TryGetValue(instanceName, out var motion))
        {
            _toastService.ShowWarning($"未找到控制器: {instanceName}");
            return;
        }

        var distance = AkribisJogStep * direction;

        IsBusy = true;
        StatusText = $"{instanceName}.{akAxis} {(direction > 0 ? "+" : "-")}...";
        NotifyOfPropertyChange(nameof(StatusText));
        NotifyOfPropertyChange(nameof(IsBusy));

        var result = await motion.MoveRelativeAsync(akAxis, distance);

        IsBusy = false;
        if (result.IsSuccess)
            StatusText = "就绪";
        else
            _toastService.ShowWarning($"{instanceName}.{akAxis} 运动失败:\n{result.Message}");
        NotifyOfPropertyChange(nameof(StatusText));
        NotifyOfPropertyChange(nameof(IsBusy));
    }

    // ========== 夹爪控制 ==========

    public Task JogGripperL(int direction) => JogGripper(GripperLName, direction);
    public Task JogGripperR(int direction) => JogGripper(GripperRName, direction);

    private async Task JogGripper(string gripperName, int direction)
    {
        if (!_grippers.TryGetValue(gripperName, out var gripper))
        {
            _toastService.ShowWarning($"未找到夹爪: {gripperName}");
            return;
        }

        var target = gripper.CurrentPosition + GripperStep * direction;
        target = Math.Clamp(target, 0, 400);

        IsBusy = true;
        StatusText = $"{gripper.DisplayName} {(direction > 0 ? "打开" : "关闭")} → {target}";
        NotifyOfPropertyChange(nameof(StatusText));
        NotifyOfPropertyChange(nameof(IsBusy));

        var result = await gripper.MoveAsync(speed: 100, position: (ushort)target);

        IsBusy = false;
        if (result.IsSuccess)
            StatusText = "就绪";
        else
            _toastService.ShowWarning($"{gripper.DisplayName} 运动失败:\n{result.Message}");
        NotifyOfPropertyChange(nameof(StatusText));
        NotifyOfPropertyChange(nameof(IsBusy));
    }
}
