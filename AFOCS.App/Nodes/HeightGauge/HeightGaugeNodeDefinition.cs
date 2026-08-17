using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Text.Json.Serialization;
using AFOCS.Devices.HeightGauge;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.HeightGauge;

/// <summary>
/// 测高仪读取节点：从指定通道读取高度值，输出 Height 给下游节点。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.HeightGauge", "测高仪读取", "设备")]
[CategoryOrder("基础", 0), 
 CategoryOrder("配置", 1), 
 CategoryOrder("输入", 2), 
 CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class HeightGaugeNodeDefinition(IHeightGauge heightGauge, ILogger logger)
    : NodeDefinitionBase, IExecutableNode
{
    // ========== 输出端口 ==========

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("Height", "高度值", NodePortType.Double, false)]
    [Category("输出")]
    public double Height { get; set; }

    // ========== 配置属性 ==========

    [DisplayName("通道")]
    [ItemsSource(typeof(HeightGaugeChannelItemsSource))]
    [Category("配置")]
    public int Channel
    {
        get;
        set => Set(ref field, value);
    } = 1;

    // ========== 执行 ==========

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        if (!heightGauge.IsConnected)
            throw new InvalidOperationException("测高仪读取节点：测高仪未连接，请先在设备配置中连接");

        var result = await heightGauge.GetHeightAsync(Channel);
        if (!result.IsSuccess)
            throw new InvalidOperationException($"测高仪读取节点：读取通道 {Channel} 失败 - {result.Message}");

        Height = result.Data;
        logger.Information("测高仪读取节点：通道 {Channel} = {Height}", Channel, result.Data);

        return new Dictionary<string, object?> { ["Height"] = result.Data };
    }
}

public class HeightGaugeChannelItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        for (int i = 1; i <= 4; i++)
            items.Add(i, $"通道 {i}");
        return items;
    }
}
