using System.ComponentModel.Composition;
using AFOCS.App.Models;
using AFOCS.Framework.Modules.Settings;
using AFOCS.Infrastructure;
using Caliburn.Micro;
using Microsoft.Win32;

namespace AFOCS.App.ViewModels.Settings;

/// <summary>左右工位生产信息设置页</summary>
[Export(typeof(ISettingsEditorAsync))]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class ProductionInfoSettingsViewModel : Screen, ISettingsEditorAsync
{
    private readonly IConfigService _configService;
    private readonly ProductionInfoConfig _config;

    [ImportingConstructor]
    public ProductionInfoSettingsViewModel(IConfigService configService)
    {
        _configService = configService;
        _config = Task.Run(() => configService.LoadAsync<ProductionInfoConfig>()).GetAwaiter().GetResult()
                  ?? new ProductionInfoConfig();
    }

    public string SettingsPageName => "生产信息";

    public string SettingsPagePath => string.Empty;

    public StationProductionInfo Left => _config.Left;

    public StationProductionInfo Right => _config.Right;

    public Task ApplyChangesAsync() => _configService.SaveAsync(_config);

    // ========== 流程地址选择 ==========

    public void BrowseLeftLogicFlow() => BrowseAndSet(nameof(Left), p => _config.Left.LogicFlowPath = p);

    public void BrowseLeftSafePositionFlow() => BrowseAndSet(nameof(Left), p => _config.Left.SafePositionFlowPath = p);

    public void BrowseLeftHomeFlow() => BrowseAndSet(nameof(Left), p => _config.Left.HomeFlowPath = p);

    public void BrowseRightLogicFlow() => BrowseAndSet(nameof(Right), p => _config.Right.LogicFlowPath = p);

    public void BrowseRightSafePositionFlow() => BrowseAndSet(nameof(Right), p => _config.Right.SafePositionFlowPath = p);

    public void BrowseRightHomeFlow() => BrowseAndSet(nameof(Right), p => _config.Right.HomeFlowPath = p);

    private void BrowseAndSet(string propertyName, System.Action<string> setPath)
    {
        var dialog = new OpenFileDialog { Filter = "流程图|*.nflow|所有文件|*.*" };
        if (dialog.ShowDialog() != true)
            return;

        setPath(dialog.FileName);
        NotifyOfPropertyChange(propertyName);
    }
}