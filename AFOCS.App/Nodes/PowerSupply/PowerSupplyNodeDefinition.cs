using AFOCS.Devices.ProgrammablePowerSupply;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Caliburn.Micro;
using Serilog;
using System.ComponentModel;
using System.ComponentModel.Composition;
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
[CategoryOrder("基础", 0), 
 CategoryOrder("配置", 1),
 CategoryOrder("输入", 2), 
 CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class PowerSupplyNodeDefinition(IProgrammablePowerSupply powerSupply)
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
    } = 3.6;

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
            throw new InvalidOperationException($"设置通道 {Channel} 电压/电流失败: {setResult.Message}");

        var statusResult = await powerSupply.SetChannelStatusAsync(Channel, OutputEnabled);
        if (!statusResult.IsSuccess)
            throw new InvalidOperationException($"设置通道 {Channel} 输出状态失败: {statusResult.Message}");
        return new Dictionary<string, object?>();
    }

}

public class PowerChannelItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection
        {
            { 1, "通道 1" },
            { 2, "通道 2" }
        };
        return items;
    }
}
