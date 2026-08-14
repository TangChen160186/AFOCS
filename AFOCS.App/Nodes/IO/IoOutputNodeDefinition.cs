using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Devices.IO;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure.Extensions;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.IO;

/// <summary>
/// IO 输出节点：选择一个输出信号，控制其打开/关闭（逻辑值，已考虑有效电平）。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.IoOutput", "IO输出", "设备")]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class IoOutputNodeDefinition(
    IIoDevice ioDevice,
    ILogger logger)
    : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("输出信号")]
    [ItemsSource(typeof(IoOutputItemsSource))]
    [Category("配置")]
    public AllOutputs Signal
    {
        get;
        set => Set(ref field, value);
    } = AllOutputs.TowerGreen;

    [DisplayName("输出状态")]
    [Category("配置")]
    public bool OutputOn
    {
        get;
        set => Set(ref field, value);
    } = true;

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        if (!ioDevice.IsConnected)
            throw new InvalidOperationException("IO 设备未连接，请先在设备配置中连接");

        var result = await ioDevice.WriteOutputAsync(Signal, OutputOn);
        if (!result.IsSuccess)
        {
            var errInfo = $"IO 输出失败: {result.Message}";
            logger.Error(errInfo);
            throw new InvalidOperationException(errInfo);
        }

        logger.Information("IO 输出 {Signal} → {State}", Signal, OutputOn ? "打开" : "关闭");
        return new Dictionary<string, object?>();
    }
}

public class IoOutputItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        foreach (var signal in Enum.GetValues<AllOutputs>())
            items.Add(signal, signal.GetDescription());
        return items;
    }
}
