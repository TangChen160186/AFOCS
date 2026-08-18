using System.ComponentModel.Composition;
using System.Threading;
using AFOCS.App.Models;
using AFOCS.Framework.Modules.Settings;
using AFOCS.Infrastructure;
using Caliburn.Micro;
using Serilog;

namespace AFOCS.App.ViewModels.Settings;

/// <summary>
/// 夹爪旋转补偿设置页：配置 X/Y/Z 三个直线轴的初始角度（度）与旋转半径（um）。
/// 旋转补偿节点读取该配置计算补偿量。
/// </summary>
[Export(typeof(ISettingsEditor))]
[PartCreationPolicy(CreationPolicy.NonShared)]

public class RotationCompensationSettingsViewModel
    : Screen, ISettingsEditor
{

    [ImportingConstructor]
    public RotationCompensationSettingsViewModel(IConfigService configService, ILogger logger)
    {
        _configService = configService;
        _logger = logger;

        Load();
    }

    private async void Load()
    {
        var loaded = await _configService.LoadAsync<GripperRotationCompensationConfig>();
        if (loaded != null)
        {
            _config.X.InitialAngle = loaded.X.InitialAngle;
            _config.X.Radius = loaded.X.Radius;
            _config.Y.InitialAngle = loaded.Y.InitialAngle;
            _config.Y.Radius = loaded.Y.Radius;
            _config.Z.InitialAngle = loaded.Z.InitialAngle;
            _config.Z.Radius = loaded.Z.Radius;
            StatusMessage = "配置已加载";
        }
        else
        {
            StatusMessage = "暂无配置，填写后点击保存";
        }

        NotifyOfPropertyChange(null);

    }
    private readonly IConfigService _configService;
    private readonly ILogger _logger;
    private readonly GripperRotationCompensationConfig _config = new();

    public string SettingsPageName => "旋转补偿";

    public string SettingsPagePath => "设备配置";

    private string _statusMessage = "";

    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    // ===== X 轴 =====

    public double XInitialAngle
    {
        get => _config.X.InitialAngle;
        set { _config.X.InitialAngle = value; NotifyOfPropertyChange(); }
    }

    public double XRadius
    {
        get => _config.X.Radius;
        set { _config.X.Radius = value; NotifyOfPropertyChange(); }
    }

    // ===== Y 轴 =====

    public double YInitialAngle
    {
        get => _config.Y.InitialAngle;
        set { _config.Y.InitialAngle = value; NotifyOfPropertyChange(); }
    }

    public double YRadius
    {
        get => _config.Y.Radius;
        set { _config.Y.Radius = value; NotifyOfPropertyChange(); }
    }

    // ===== Z 轴 =====

    public double ZInitialAngle
    {
        get => _config.Z.InitialAngle;
        set { _config.Z.InitialAngle = value; NotifyOfPropertyChange(); }
    }

    public double ZRadius
    {
        get => _config.Z.Radius;
        set { _config.Z.Radius = value; NotifyOfPropertyChange(); }
    }


    public async Task SaveAsync()
    {
        var ok = await _configService.SaveAsync(_config);
        StatusMessage = ok ? "配置已保存" : "保存失败，请查看日志";
        _logger.Information("夹爪旋转补偿配置保存结果: {Result}", ok);
    }

    public void ApplyChanges() => _ = SaveAsync();
}
