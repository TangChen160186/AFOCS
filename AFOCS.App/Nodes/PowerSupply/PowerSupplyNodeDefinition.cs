using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Devices.ProgrammablePowerSupply;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.PowerSupply;

/// <summary>
/// 电源输出节点：设置通道电压/电流，并控制通道输出开关。
/// 先设置电压电流，再切换输出状态（与设置界面 ApplyChannel 顺序一致）。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.PowerSupply", "电源输出", "设备")]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class PowerSupplyNodeDefinition(
    IProgrammablePowerSupply powerSupply,
    ILogger logger)
    : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("通道")]
    [ItemsSource(typeof(PowerChannelItemsSource))]
    [Category("配置")]
    public int Channel
    {
        get;
        set => Set(ref field, value);
    } = 1;

    [DisplayName("输出使能")]
    [Category("配置")]
    public bool OutputEnabled
    {
        get;
        set => Set(ref field, value);
    } = true;

    [DisplayName("电压(V)")]
    [Category("配置")]
    public double Voltage
    {
        get;
        set => Set(ref field, value);
    } = 3.0;

    [DisplayName("电流(A)")]
    [Category("配置")]
    public double Current
    {
        get;
        set => Set(ref field, value);
    } = 1.0;

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        if (!powerSupply.IsConnected)
            throw new InvalidOperationException("可编程电源未连接，请先在设备配置中连接");

        var setResult = await powerSupply.SetVoltageAndCurrentAsync(Channel, Voltage, Current);
        if (!setResult.IsSuccess)
            throw LogError($"设置通道 {Channel} 电压/电流失败: {setResult.Message}");

        var statusResult = await powerSupply.SetChannelStatusAsync(Channel, OutputEnabled);
        if (!statusResult.IsSuccess)
            throw LogError($"设置通道 {Channel} 输出状态失败: {statusResult.Message}");

        logger.Information("电源通道 {Channel} 输出 {State}，电压 {Voltage}V，电流 {Current}A",
            Channel, OutputEnabled ? "打开" : "关闭", Voltage, Current);
        return new Dictionary<string, object?>();
    }

    private InvalidOperationException LogError(string message)
    {
        logger.Error(message);
        return new InvalidOperationException(message);
    }
}

public class PowerChannelItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        items.Add(1, "通道 1");
        items.Add(2, "通道 2");
        return items;
    }
}
