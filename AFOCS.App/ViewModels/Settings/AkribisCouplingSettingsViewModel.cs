using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Windows;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Modules.Settings;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels.Settings;

/// <summary>雅克贝斯耦合设置基类 —— 每个子类对应一个物理工位</summary>
public abstract class AkribisCouplingSettingsBase(string name, IAkribisMotion motion)
    : Screen, ISettingsEditor
{
    private readonly IAkribisMotion _motion = motion;

    public string Name { get; } = name;

    string ISettingsEditor.SettingsPageName => Name;
    string ISettingsEditor.SettingsPagePath => "设备配置\\雅克贝斯板卡";

    // ========== 编辑副本 ==========

    private AkribisCouplingConfig _editConfig = motion.GetConfig();
    private bool _isModify;

    private readonly string[] _modifyProperties =
    [
        nameof(Ip), nameof(Ark), nameof(AutoReconnect),
        nameof(Speed), nameof(Accel), nameof(Decel),
    ];

    public ObservableCollection<AkribisAxisId> AxisList { get; } =
        new([AkribisAxisId.X, AkribisAxisId.Y, AkribisAxisId.Z]);

    private AkribisAxisId _selectedAxis = AkribisAxisId.X;

    public AkribisAxisId SelectedAxis
    {
        get => _selectedAxis;
        set
        {
            if (_selectedAxis == value) return;
            _selectedAxis = value;
            NotifyOfPropertyChange();
            RefreshAxisProperties();
        }
    }

    // ========== 连接参数 ==========

    public string Ip
    {
        get => _editConfig.Ip;
        set { _editConfig.Ip = value; NotifyOfPropertyChange(); }
    }

    public bool Ark
    {
        get => _editConfig.Ark;
        set { _editConfig.Ark = value; NotifyOfPropertyChange(); }
    }

    public bool AutoReconnect
    {
        get => _editConfig.AutoReconnect;
        set { _editConfig.AutoReconnect = value; NotifyOfPropertyChange(); }
    }

    // ========== 轴参数（按 SelectedAxis） ==========

    public int Speed
    {
        get => GetAxisParams().Speed;
        set { GetAxisParams().Speed = value; NotifyOfPropertyChange(); }
    }

    public int Accel
    {
        get => GetAxisParams().Accel;
        set { GetAxisParams().Accel = value; NotifyOfPropertyChange(); }
    }

    public int Decel
    {
        get => GetAxisParams().Decel;
        set { GetAxisParams().Decel = value; NotifyOfPropertyChange(); }
    }


    // ========== 实时位置 ==========

    public int PositionX
    {
        get;
        set => Set(ref field, value);
    }

    public int PositionY
    {
        get;
        set => Set(ref field, value);
    }

    public int PositionZ
    {
        get;
        set => Set(ref field, value);
    }

    // ========== 状态 ==========

    public bool AreConnected => _motion.IsConnected;

    public bool IsMonitoringDisplay => _motion.IsMonitoring;

    public string StatusMessage
    {
        get;
        set => Set(ref field, value);
    } = "";

    public bool IsModify => _isModify;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; NotifyOfPropertyChange(); }
    }

    // ========== 事件 ==========

    private void OnPositionChanged(object? sender, AkribisPositionChangedEventArgs e)
    {
        PositionX = e.X;
        PositionY = e.Y;
        PositionZ = e.Z;
    }

    private void RefreshStatus()
    {
        StatusMessage = _motion.IsConnected
            ? (_motion.IsMonitoring ? "已连接 · 监控中" : "已连接 · 未监控")
            : "未连接";
    }

    // ========== 生命周期（注册 / 解注册） ==========

    protected override void OnViewAttached(object view, object context)
    {
        base.OnViewAttached(view, context);

        Subscribe();
        RefreshStatus();

        if (view is FrameworkElement fe)
            fe.Unloaded += OnViewUnloaded;
    }

    private void OnViewUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
            fe.Unloaded -= OnViewUnloaded;
        Unsubscribe();
    }

    private void Subscribe()
    {
        _motion.PositionChanged += OnPositionChanged;
    }

    private void Unsubscribe()
    {
        _motion.PositionChanged -= OnPositionChanged;
    }

    // ========== 操作 ==========

    public async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = "保存中...";
        try
        {
            await _motion.SaveConfigAsync(_editConfig);
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

    public void Reset()
    {
        _editConfig = _motion.GetConfig();
        _isModify = true;
        NotifyOfPropertyChange(nameof(IsModify));
        RefreshAllProperties();
        StatusMessage = "已重置为设备当前值";
    }

    public async Task ReconnectAsync()
    {
        IsBusy = true;
        StatusMessage = "正在重连...";
        try
        {
            var r = await _motion.ReConnectAsync();
            StatusMessage = r.IsSuccess ? "重连成功" : $"重连失败: {r.Message}";
            RefreshStatus();
        }
        catch (Exception ex)
        {
            StatusMessage = $"重连异常: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ApplyChanges()
    {
        if (_isModify) _ = SaveAsync();
    }

    // ========== 运动测试 ==========

    public ObservableCollection<AkribisAxisId> TestAxisList { get; } =
        new([AkribisAxisId.X, AkribisAxisId.Y, AkribisAxisId.Z]);

    private AkribisAxisId _testAxis = AkribisAxisId.X;
    public AkribisAxisId TestAxis
    {
        get => _testAxis;
        set
        {
            if (_testAxis == value) return;
            _testAxis = value;
            NotifyOfPropertyChange();
        }
    }

    private int _testDistance = 1000;
    public int TestDistance
    {
        get => _testDistance;
        set { _testDistance = value; NotifyOfPropertyChange(); }
    }

    public async Task JogPlusAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = $"{_testAxis} 正向运动 {_testDistance} ...";
        var r = await _motion.MoveRelativeAsync(_testAxis, _testDistance);
        StatusMessage = r.IsSuccess ? $"{_testAxis} 正向运动完成" : $"运动失败: {r.Message}";
        IsBusy = false;
    }

    public async Task JogMinusAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = $"{_testAxis} 反向运动 {-TestDistance} ...";
        var r = await _motion.MoveRelativeAsync(_testAxis, -TestDistance);
        StatusMessage = r.IsSuccess ? $"{_testAxis} 反向运动完成" : $"运动失败: {r.Message}";
        IsBusy = false;
    }

    public async Task TestEnableAsync()
    {
        StatusMessage = $"{_testAxis} 使能中...";
        var r = await _motion.EnableAsync(_testAxis);
        StatusMessage = r.IsSuccess ? $"{_testAxis} 使能完成" : $"使能失败: {r.Message}";
    }

    public async Task TestDisEnableAsync()
    {
        StatusMessage = $"{_testAxis} 关使能中...";
        var r = await _motion.DisEnableAsync(_testAxis);
        StatusMessage = r.IsSuccess ? $"{_testAxis} 关使能完成" : $"关使能失败: {r.Message}";
    }

    public async Task TestHomeAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = $"{_testAxis} 回零中...";
        var r = await _motion.HomeAsync(_testAxis);
        StatusMessage = r.IsSuccess ? $"{_testAxis} 回零完成" : $"回零失败: {r.Message}";
        IsBusy = false;
    }

    public async Task TestStopAsync()
    {
        StatusMessage = $"{_testAxis} 停止中...";
        var r = await _motion.StopAxisAsync(_testAxis);
        StatusMessage = r.IsSuccess ? $"{_testAxis} 已停止" : $"停止失败: {r.Message}";
    }

    public async Task TestEmergencyStopAsync()
    {
        StatusMessage = $"{_testAxis} 急停中...";
        var r = await _motion.EmergencyStopAsync(_testAxis);
        StatusMessage = r.IsSuccess ? $"{_testAxis} 已急停" : $"急停失败: {r.Message}";
    }

    public async Task TestEmergencyStopAllAsync()
    {
        StatusMessage = "全部急停中...";
        var r = await _motion.EmergencyStopAllAsync();
        StatusMessage = r.IsSuccess ? "全部急停完成" : $"急停失败: {r.Message}";
    }

    // ========== 内部辅助 ==========

    private AkribisAxisParams GetAxisParams() => _selectedAxis switch
    {
        AkribisAxisId.X => _editConfig.XAxis,
        AkribisAxisId.Y => _editConfig.YAxis,
        AkribisAxisId.Z => _editConfig.ZAxis,
        _ => throw new ArgumentOutOfRangeException()
    };

    private void RefreshAllProperties()
    {
        NotifyOfPropertyChange(nameof(Ip));
        NotifyOfPropertyChange(nameof(Ark));
        NotifyOfPropertyChange(nameof(AutoReconnect));
        RefreshAxisProperties();
    }

    private void RefreshAxisProperties()
    {
        NotifyOfPropertyChange(nameof(Speed));
        NotifyOfPropertyChange(nameof(Accel));
        NotifyOfPropertyChange(nameof(Decel));
    }

    public override void NotifyOfPropertyChange([CallerMemberName] string? propertyName = null)
    {
        if (_modifyProperties.Contains(propertyName))
            _isModify = true;
        base.NotifyOfPropertyChange(propertyName);
    }
}

// ====================================================================
// 4 个子类 —— 每个对应一个物理工位，MEF 导出为独立设置页
// ====================================================================

[Export(typeof(ISettingsEditor))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[method: ImportingConstructor]
public class AkribisLeftCouplingLSettingsViewModel(AkribisLeftCouplingL motion)
    : AkribisCouplingSettingsBase("左工位左耦合", motion);

[Export(typeof(ISettingsEditor))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[method: ImportingConstructor]
public class AkribisLeftCouplingRSettingsViewModel(AkribisLeftCouplingR motion)
    : AkribisCouplingSettingsBase("左工位右耦合", motion);

[Export(typeof(ISettingsEditor))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[method: ImportingConstructor]
public class AkribisRightCouplingLSettingsViewModel(AkribisRightCouplingL motion)
    : AkribisCouplingSettingsBase("右工位左耦合", motion);

[Export(typeof(ISettingsEditor))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[method: ImportingConstructor]
public class AkribisRightCouplingRSettingsViewModel(AkribisRightCouplingR motion)
    : AkribisCouplingSettingsBase("右工位右耦合", motion);
