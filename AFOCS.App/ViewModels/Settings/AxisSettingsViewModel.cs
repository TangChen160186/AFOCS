using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Windows;
using AFOCS.Devices.BusAxisDevice;
using AFOCS.Framework.Modules.Settings;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels.Settings;

[Export(typeof(ISettingsEditor))]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class AxisSettingsViewModel : Screen, ISettingsEditor
{
    private readonly IBusAxisDevice _busAxisDevice;
    private BusAxisId _selectedBusAxis;
    private AxisConfig _currentConfig = new();
    private bool _isModify;

    private readonly string[] _modifyProperties =
    [
        nameof(Equiv), nameof(MinVel), nameof(MaxVel), nameof(Tacc), nameof(Tdec),
        nameof(StopVel), nameof(SPara),
        nameof(HomeMode), nameof(HomeLowVel), nameof(HomeHighVel),
        nameof(HomeTacc), nameof(HomeTdec), nameof(HomeOffsetPos),
        nameof(NegativeSoftLimit), nameof(PositiveSoftLimit), nameof(SoftLimitEnabled),
    ];

    [ImportingConstructor]
    public AxisSettingsViewModel(IBusAxisDevice busAxisDevice)
    {
        _busAxisDevice = busAxisDevice;

        AxisList = new ObservableCollection<AxisInfo>(
            Enum.GetValues<BusAxisId>().Select(a => new AxisInfo
            {
                BusAxisId = a,
                DisplayName = BusAxisDevice.GetAxisDisplayName(a),
                AxisNumber = (int)a
            }));

        SelectedAxisInfo = AxisList.FirstOrDefault();
        _ = InitializeAsync();
    }

    public string SettingsPageName => "总线轴配置";
    public string SettingsPagePath => "设备配置\\雷赛板卡";

    // ========== 生命周期 ==========

    protected override void OnViewAttached(object view, object context)
    {
        base.OnViewAttached(view, context);
        if (view is FrameworkElement fe)
            fe.Unloaded += OnViewUnloaded;
    }

    private void OnViewUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
            fe.Unloaded -= OnViewUnloaded;
    }

    // ========== 轴列表 ==========

    public ObservableCollection<AxisInfo> AxisList { get; }

    private AxisInfo? _selectedAxisInfo;
    public AxisInfo? SelectedAxisInfo
    {
        get => _selectedAxisInfo;
        set
        {
            if (_selectedAxisInfo == value) return;
            _selectedAxisInfo = value;
            NotifyOfPropertyChange();
            if (value != null)
            {
                _selectedBusAxis = value.BusAxisId;
                LoadAxisConfig(value.BusAxisId);
            }
        }
    }

    public string StatusMessage
    {
        get;
        set
        {
            field = value;
            NotifyOfPropertyChange();
        }
    } = string.Empty;

    public bool IsBusy
    {
        get;
        set
        {
            field = value;
            NotifyOfPropertyChange();
        }
    }

    public bool IsModify => _isModify;

    // ========== 运动测试 ==========

    private double _moveDistance = 10;
    public double MoveDistance
    {
        get => _moveDistance;
        set { _moveDistance = value; NotifyOfPropertyChange(); }
    }

    private bool _movePositive = true;
    public bool MovePositive
    {
        get => _movePositive;
        set { _movePositive = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(MoveDirectionText)); }
    }

    public string MoveDirectionText => _movePositive ? "正向 (+)" : "反向 (-)";

    private bool _isMoving;
    public bool IsMoving
    {
        get => _isMoving;
        set { _isMoving = value; NotifyOfPropertyChange(); }
    }

    // ========== 运动参数 ==========

    public double Equiv
    {
        get => _currentConfig.Motion.Equiv;
        set { _currentConfig.Motion.Equiv = value; NotifyOfPropertyChange(); }
    }
    public double MinVel
    {
        get => _currentConfig.Motion.MinVel;
        set { _currentConfig.Motion.MinVel = value; NotifyOfPropertyChange(); }
    }
    public double MaxVel
    {
        get => _currentConfig.Motion.MaxVel;
        set { _currentConfig.Motion.MaxVel = value; NotifyOfPropertyChange(); }
    }
    public double Tacc
    {
        get => _currentConfig.Motion.Tacc;
        set { _currentConfig.Motion.Tacc = value; NotifyOfPropertyChange(); }
    }
    public double Tdec
    {
        get => _currentConfig.Motion.Tdec;
        set { _currentConfig.Motion.Tdec = value; NotifyOfPropertyChange(); }
    }
    public double StopVel
    {
        get => _currentConfig.Motion.StopVel;
        set { _currentConfig.Motion.StopVel = value; NotifyOfPropertyChange(); }
    }
    public double SPara
    {
        get => _currentConfig.Motion.SPara;
        set { _currentConfig.Motion.SPara = value; NotifyOfPropertyChange(); }
    }

    // ========== 回零参数 ==========

    public ushort HomeMode
    {
        get => _currentConfig.Home.HomeMode;
        set { _currentConfig.Home.HomeMode = value; NotifyOfPropertyChange(); }
    }
    public double HomeLowVel
    {
        get => _currentConfig.Home.LowVel;
        set { _currentConfig.Home.LowVel = value; NotifyOfPropertyChange(); }
    }
    public double HomeHighVel
    {
        get => _currentConfig.Home.HighVel;
        set { _currentConfig.Home.HighVel = value; NotifyOfPropertyChange(); }
    }
    public double HomeTacc
    {
        get => _currentConfig.Home.Tacc;
        set { _currentConfig.Home.Tacc = value; NotifyOfPropertyChange(); }
    }
    public double HomeTdec
    {
        get => _currentConfig.Home.Tdec;
        set { _currentConfig.Home.Tdec = value; NotifyOfPropertyChange(); }
    }
    public double HomeOffsetPos
    {
        get => _currentConfig.Home.OffsetPos;
        set { _currentConfig.Home.OffsetPos = value; NotifyOfPropertyChange(); }
    }

    // ========== 软限位 ==========

    public double NegativeSoftLimit
    {
        get => _currentConfig.NegativeSoftLimit;
        set { _currentConfig.NegativeSoftLimit = value; NotifyOfPropertyChange(); }
    }
    public double PositiveSoftLimit
    {
        get => _currentConfig.PositiveSoftLimit;
        set { _currentConfig.PositiveSoftLimit = value; NotifyOfPropertyChange(); }
    }
    public bool SoftLimitEnabled
    {
        get => _currentConfig.SoftLimitEnabled;
        set { _currentConfig.SoftLimitEnabled = value; NotifyOfPropertyChange(); }
    }

    // ========== 其他 ==========

    public double MaxSpeed
    {
        get => _currentConfig.MaxSpeed;
        set { _currentConfig.MaxSpeed = value; NotifyOfPropertyChange(); }
    }

    // ========== 回零模式选项 ==========

    public ObservableCollection<HomeModeOption> HomeModeOptions { get; } = new()
    {
        new(33, "正向找Z相"),
        new(34, "负向找Z相"),
        new(1,  "找负限位反找Z相"),
        new(2,  "找正限位反找Z相"),
        new(17, "找负限位"),
        new(18, "找正限位"),
        new(35, "当前位置设为原点"),
    };

    // ========== 操作 ==========

    public async Task SaveCurrentAxisAsync()
    {
        IsBusy = true;
        StatusMessage = "保存中...";
        try
        {
            _busAxisDevice.SetAxisConfig(_selectedBusAxis, _currentConfig);
            await _busAxisDevice.SaveAllAxisConfigsAsync();
            _isModify = false;
            NotifyOfPropertyChange(nameof(IsModify));
            StatusMessage = "已保存";
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

    public void ResetToDefault()
    {
        var defaults = _busAxisDevice.GetDefaultAxisConfig(_selectedBusAxis);
        _currentConfig = defaults.Clone();
        _isModify = true;
        NotifyOfPropertyChange(nameof(IsModify));
        RefreshAllProperties();
        StatusMessage = "已重置为默认值";
    }

    public async Task MoveTestAsync()
    {
        if (_busAxisDevice == null || !_busAxisDevice.IsConnected)
        {
            StatusMessage = "总线轴设备未连接";
            return;
        }

        IsMoving = true;
        StatusMessage = "运动中...";
        try
        {
            var distance = _movePositive ? _moveDistance : -_moveDistance;
            var result = await _busAxisDevice.MovePmoveAsync(
                busAxisId: _selectedBusAxis,
                distance: distance);

            if (result.IsSuccess)
                StatusMessage = "移动完成";
            else
                StatusMessage = $"移动失败: {result.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"移动异常: {ex.Message}";
        }
        finally
        {
            IsMoving = false;
        }
    }

    public async Task StopAsync()
    {
        if (_busAxisDevice == null || !_busAxisDevice.IsConnected)
        {
            StatusMessage = "总线轴设备未连接";
            return;
        }

        StatusMessage = "停止中...";
        var result = await _busAxisDevice.StopAxisAsync(_selectedBusAxis);
        StatusMessage = result.IsSuccess ? "已停止" : $"停止失败: {result.Message}";
        IsMoving = false;
    }

    // ========== NotifyOfPropertyChange 重写 ==========

    public override void NotifyOfPropertyChange([CallerMemberName] string? propertyName = null)
    {
        base.NotifyOfPropertyChange(propertyName);

        if (_modifyProperties.Contains(propertyName))
        {
            _isModify = true;
            NotifyOfPropertyChange(nameof(IsModify));
        }
    }

    // ========== ISettingsEditor ==========

    public void ApplyChanges()
    {
        if (_isModify) _ = SaveCurrentAxisAsync();
    }

    // ========== 内部方法 ==========

    private async Task InitializeAsync()
    {
        if (SelectedAxisInfo != null)
            LoadAxisConfig(SelectedAxisInfo.BusAxisId);
    }

    private void LoadAxisConfig(BusAxisId busAxisId)
    {
        var config = _busAxisDevice.GetAxisConfig(busAxisId);
        _currentConfig = config.Clone();
        _isModify = false;
        NotifyOfPropertyChange(nameof(IsModify));
        RefreshAllProperties();
    }

    private void RefreshAllProperties()
    {
        NotifyOfPropertyChange(nameof(Equiv));
        NotifyOfPropertyChange(nameof(MinVel));
        NotifyOfPropertyChange(nameof(MaxVel));
        NotifyOfPropertyChange(nameof(Tacc));
        NotifyOfPropertyChange(nameof(Tdec));
        NotifyOfPropertyChange(nameof(StopVel));
        NotifyOfPropertyChange(nameof(SPara));
        NotifyOfPropertyChange(nameof(HomeMode));
        NotifyOfPropertyChange(nameof(HomeLowVel));
        NotifyOfPropertyChange(nameof(HomeHighVel));
        NotifyOfPropertyChange(nameof(HomeTacc));
        NotifyOfPropertyChange(nameof(HomeTdec));
        NotifyOfPropertyChange(nameof(HomeOffsetPos));
        NotifyOfPropertyChange(nameof(NegativeSoftLimit));
        NotifyOfPropertyChange(nameof(PositiveSoftLimit));
        NotifyOfPropertyChange(nameof(SoftLimitEnabled));
        NotifyOfPropertyChange(nameof(MaxSpeed));
    }
}

// ========== 辅助类型 ==========

public class AxisInfo
{
    public BusAxisId BusAxisId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int AxisNumber { get; set; }
    public string HeaderText => $"Axis{AxisNumber} — {DisplayName}";
}

public class HomeModeOption
{
    public ushort Mode { get; }
    public string Label { get; }

    public HomeModeOption(ushort mode, string label)
    {
        Mode = mode;
        Label = $"{mode} — {label}";
    }
}