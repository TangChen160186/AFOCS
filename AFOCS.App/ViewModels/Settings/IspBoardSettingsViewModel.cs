using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using AFOCS.Devices.IspBoard;
using AFOCS.Framework.Modules.Settings;
using AFOCS.Infrastructure;
using Caliburn.Micro;
using Microsoft.Win32;

namespace AFOCS.App.ViewModels.Settings;

[Export(typeof(ISettingsEditor))]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class IspBoardSettingsViewModel : Screen, ISettingsEditor
{
    private const int ChannelCount = 8;

    private readonly IIspBoardDevice _device;
    private readonly IConfigService _configService;
    private IspBoardConfig _config = new();
    private bool _isModify;

    private readonly string[] _modifyProperties =
    [
        nameof(ProductCfgFilePath),
        nameof(IpsnAppName), nameof(RxAdcAppName), nameof(RxAdcFormulaAppName),
        nameof(MpdInAppName), nameof(MpdOutAppName),
        nameof(RspPollingIntervalMs),
        nameof(LeftDeviceId), nameof(LeftDutSlot), nameof(LeftDutChannel),
        nameof(RightDeviceId), nameof(RightDutSlot), nameof(RightDutChannel),
    ];

    // 通道光功率显示值（dB），内部存储为 mW
    private readonly double[] _leftChDb = new double[ChannelCount];
    private readonly double[] _rightChDb = new double[ChannelCount];

    [ImportingConstructor]
    public IspBoardSettingsViewModel(IIspBoardDevice device, IConfigService configService)
    {
        _device = device;
        _configService = configService;

        for (int i = 0; i < ChannelCount; i++)
        {
            int idx = i;
            _modifyProperties = [.. _modifyProperties, $"LeftCh{idx}dB", $"RightCh{idx}dB"];
        }
    }

    public string SettingsPageName => "ISP Board";
    public string SettingsPagePath => "设备配置";

    protected override async void OnViewAttached(object view, object context)
    {
        base.OnViewAttached(view, context);

        var config = await _configService.LoadAsync<IspBoardConfig>();
        if (config != null)
        {
            _config = config;
            LoadFromConfig();
        }

        RefreshConnectionStatus();
    }

    // ========== 连接状态 ==========

    public bool IsConnected => _device.IsConnected;

    // ========== 基础配置 ==========

    public string ProductCfgFilePath
    {
        get => _config.ProductCfgFilePath;
        set { if (_config.ProductCfgFilePath == value) return; _config.ProductCfgFilePath = value; NotifyOfPropertyChange(); }
    }

    public int RspPollingIntervalMs
    {
        get => _config.RspPollingIntervalMs;
        set { if (_config.RspPollingIntervalMs == value) return; _config.RspPollingIntervalMs = value; NotifyOfPropertyChange(); }
    }

    public string IpsnAppName
    {
        get => _config.IpsnAppName;
        set { if (_config.IpsnAppName == value) return; _config.IpsnAppName = value; NotifyOfPropertyChange(); }
    }
    public string RxAdcAppName
    {
        get => _config.RxAdcAppName;
        set { if (_config.RxAdcAppName == value) return; _config.RxAdcAppName = value; NotifyOfPropertyChange(); }
    }
    public string RxAdcFormulaAppName
    {
        get => _config.RxAdcFormulaAppName;
        set { if (_config.RxAdcFormulaAppName == value) return; _config.RxAdcFormulaAppName = value; NotifyOfPropertyChange(); }
    }
    public string MpdInAppName
    {
        get => _config.MpdInAppName;
        set { if (_config.MpdInAppName == value) return; _config.MpdInAppName = value; NotifyOfPropertyChange(); }
    }
    public string MpdOutAppName
    {
        get => _config.MpdOutAppName;
        set { if (_config.MpdOutAppName == value) return; _config.MpdOutAppName = value; NotifyOfPropertyChange(); }
    }

    // ========== 左工位 ==========

    public int LeftDeviceId
    {
        get => _config.Left.DeviceId;
        set { if (_config.Left.DeviceId == value) return; _config.Left.DeviceId = value; NotifyOfPropertyChange(); }
    }
    public int LeftDutSlot
    {
        get => _config.Left.DutSlot;
        set { if (_config.Left.DutSlot == value) return; _config.Left.DutSlot = value; NotifyOfPropertyChange(); }
    }
    public int LeftDutChannel
    {
        get => _config.Left.DutChannel;
        set { if (_config.Left.DutChannel == value) return; _config.Left.DutChannel = value; NotifyOfPropertyChange(); }
    }

    // 左工位 8 通道光功率（界面 dB ↔ 存储 mW）
    public double LeftCh0dB  { get => _leftChDb[0];  set { if (SetChDb(_leftChDb, _config.Left.ChannelLight, 0, value))  NotifyOfPropertyChange(); } }
    public double LeftCh1dB  { get => _leftChDb[1];  set { if (SetChDb(_leftChDb, _config.Left.ChannelLight, 1, value))  NotifyOfPropertyChange(); } }
    public double LeftCh2dB  { get => _leftChDb[2];  set { if (SetChDb(_leftChDb, _config.Left.ChannelLight, 2, value))  NotifyOfPropertyChange(); } }
    public double LeftCh3dB  { get => _leftChDb[3];  set { if (SetChDb(_leftChDb, _config.Left.ChannelLight, 3, value))  NotifyOfPropertyChange(); } }
    public double LeftCh4dB  { get => _leftChDb[4];  set { if (SetChDb(_leftChDb, _config.Left.ChannelLight, 4, value))  NotifyOfPropertyChange(); } }
    public double LeftCh5dB  { get => _leftChDb[5];  set { if (SetChDb(_leftChDb, _config.Left.ChannelLight, 5, value))  NotifyOfPropertyChange(); } }
    public double LeftCh6dB  { get => _leftChDb[6];  set { if (SetChDb(_leftChDb, _config.Left.ChannelLight, 6, value))  NotifyOfPropertyChange(); } }
    public double LeftCh7dB  { get => _leftChDb[7];  set { if (SetChDb(_leftChDb, _config.Left.ChannelLight, 7, value))  NotifyOfPropertyChange(); } }

    // ========== 右工位 ==========

    public int RightDeviceId
    {
        get => _config.Right.DeviceId;
        set { if (_config.Right.DeviceId == value) return; _config.Right.DeviceId = value; NotifyOfPropertyChange(); }
    }
    public int RightDutSlot
    {
        get => _config.Right.DutSlot;
        set { if (_config.Right.DutSlot == value) return; _config.Right.DutSlot = value; NotifyOfPropertyChange(); }
    }
    public int RightDutChannel
    {
        get => _config.Right.DutChannel;
        set { if (_config.Right.DutChannel == value) return; _config.Right.DutChannel = value; NotifyOfPropertyChange(); }
    }

    public double RightCh0dB { get => _rightChDb[0]; set { if (SetChDb(_rightChDb, _config.Right.ChannelLight, 0, value)) NotifyOfPropertyChange(); } }
    public double RightCh1dB { get => _rightChDb[1]; set { if (SetChDb(_rightChDb, _config.Right.ChannelLight, 1, value)) NotifyOfPropertyChange(); } }
    public double RightCh2dB { get => _rightChDb[2]; set { if (SetChDb(_rightChDb, _config.Right.ChannelLight, 2, value)) NotifyOfPropertyChange(); } }
    public double RightCh3dB { get => _rightChDb[3]; set { if (SetChDb(_rightChDb, _config.Right.ChannelLight, 3, value)) NotifyOfPropertyChange(); } }
    public double RightCh4dB { get => _rightChDb[4]; set { if (SetChDb(_rightChDb, _config.Right.ChannelLight, 4, value)) NotifyOfPropertyChange(); } }
    public double RightCh5dB { get => _rightChDb[5]; set { if (SetChDb(_rightChDb, _config.Right.ChannelLight, 5, value)) NotifyOfPropertyChange(); } }
    public double RightCh6dB { get => _rightChDb[6]; set { if (SetChDb(_rightChDb, _config.Right.ChannelLight, 6, value)) NotifyOfPropertyChange(); } }
    public double RightCh7dB { get => _rightChDb[7]; set { if (SetChDb(_rightChDb, _config.Right.ChannelLight, 7, value)) NotifyOfPropertyChange(); } }

    // ========== 状态 ==========

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { if (_isBusy == value) return; _isBusy = value; NotifyOfPropertyChange(); }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set { if (_statusMessage == value) return; _statusMessage = value; NotifyOfPropertyChange(); }
    }

    public void RefreshConnectionStatus()
    {
        NotifyOfPropertyChange(() => IsConnected);
        StatusMessage = IsConnected ? "已连接" : "未连接";
    }

    // ========== 操作 ==========

    public void BrowseProductCfg()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "INI 配置文件 (*.ini)|*.ini|所有文件 (*.*)|*.*",
            Title = "选择产品配置文件",
        };
        if (dlg.ShowDialog() == true)
            ProductCfgFilePath = dlg.FileName;
    }

    public async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = "正在保存...";
        try
        {
            await _configService.SaveAsync(_config);
            _isModify = false;
            StatusMessage = "配置已保存";
        }
        catch (Exception ex) { StatusMessage = $"保存异常: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    public async Task ReconnectAsync()
    {
        IsBusy = true;
        StatusMessage = "正在重连...";
        try
        {
            if (_isModify)
                await _configService.SaveAsync(_config);

            var result = await _device.ReConnectAsync();
            StatusMessage = result.IsSuccess ? "重连成功" : $"重连失败: {result.Message}";
        }
        catch (Exception ex) { StatusMessage = $"重连异常: {ex.Message}"; }
        finally
        {
            IsBusy = false;
            NotifyOfPropertyChange(() => IsConnected);
        }
    }

    // ========== ISettingsEditor ==========

    public void ApplyChanges()
    {
        if (_isModify) _ = SaveAsync();
    }

    // ========== NotifyOfPropertyChange ==========

    public override void NotifyOfPropertyChange([CallerMemberName] string? propertyName = null)
    {
        base.NotifyOfPropertyChange(propertyName);
        if (_modifyProperties.Contains(propertyName))
            _isModify = true;
    }

    // ========== 辅助 ==========

    private void LoadFromConfig()
    {
        NotifyOfPropertyChange(nameof(ProductCfgFilePath));
        NotifyOfPropertyChange(nameof(RspPollingIntervalMs));
        NotifyOfPropertyChange(nameof(IpsnAppName));
        NotifyOfPropertyChange(nameof(RxAdcAppName));
        NotifyOfPropertyChange(nameof(RxAdcFormulaAppName));
        NotifyOfPropertyChange(nameof(MpdInAppName));
        NotifyOfPropertyChange(nameof(MpdOutAppName));
        NotifyOfPropertyChange(nameof(LeftDeviceId));
        NotifyOfPropertyChange(nameof(LeftDutSlot));
        NotifyOfPropertyChange(nameof(LeftDutChannel));
        NotifyOfPropertyChange(nameof(RightDeviceId));
        NotifyOfPropertyChange(nameof(RightDutSlot));
        NotifyOfPropertyChange(nameof(RightDutChannel));

        LoadChDbFromMw(_leftChDb, _config.Left.ChannelLight);
        LoadChDbFromMw(_rightChDb, _config.Right.ChannelLight);
        for (int i = 0; i < ChannelCount; i++)
        {
            NotifyOfPropertyChange($"LeftCh{i}dB");
            NotifyOfPropertyChange($"RightCh{i}dB");
        }
    }

    /// <summary>读取 mW 数组转为 dB 显示数组</summary>
    private static void LoadChDbFromMw(double[] dbDisplay, double[] mwStored)
    {
        for (int i = 0; i < ChannelCount; i++)
            dbDisplay[i] = i < mwStored.Length ? MwToDb(mwStored[i]) : 0;
    }

    /// <summary>设置一个通道的 dB 值，同步转换为 mW 写入 config。返回是否实际变更。</summary>
    private static bool SetChDb(double[] dbDisplay, double[] mwStored, int ch, double dbValue)
    {
        if (Math.Abs(dbDisplay[ch] - dbValue) < 0.001) return false;
        dbDisplay[ch] = dbValue;
        if (ch < mwStored.Length)
            mwStored[ch] = DbToMw(dbValue);
        return true;
    }

    /// <summary>dB → mW: P(mW) = 10^(dB/10)</summary>
    private static double DbToMw(double db) => Math.Pow(10, db / 10.0);

    /// <summary>mW → dB: dB = 10 * log10(P/mW)</summary>
    private static double MwToDb(double mw) => mw > 0 ? 10 * Math.Log10(mw) : -100;
}
