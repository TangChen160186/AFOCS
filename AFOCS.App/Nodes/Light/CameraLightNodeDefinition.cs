using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.DataAnnotations;
using AFOCS.Devices.CameraLight;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Light;

/// <summary>
/// 设置光源亮度节点：选择光源控制器通道并设置亮度（0~255）。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.CameraLight", "设置光源亮度", "设备")]
[method: ImportingConstructor]
public class CameraLightNodeDefinition(
    ICameraLight cameraLight,
    ILogger logger) : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("通道")]
    [ItemsSource(typeof(LightChannelItemsSource))]
    public CameraLightChannel Channel
    {
        get;
        set => Set(ref field, value);
    } = CameraLightChannel.A;

    [DisplayName("亮度(0-255)")]
    [Range(0, 255)]
    public uint Brightness
    {
        get;
        set => Set(ref field, value);
    } = 128;

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        if (!cameraLight.IsConnected)
            throw new InvalidOperationException("光源控制器未连接，请先在设备配置中连接");

        var result = await cameraLight.SetLightBrightnessAsync(Channel, Brightness);
        if (!result.IsSuccess)
        {
            var errInfo = $"设置光源亮度失败: {result.Message}";
            logger.Error(errInfo);
            throw new InvalidOperationException(errInfo);
        }

        logger.Information("设置光源亮度: 通道 {Channel} = {Brightness}", Channel, Brightness);
        return new Dictionary<string, object?>();
    }
}

public class LightChannelItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        foreach (var channel in Enum.GetValues<CameraLightChannel>())
            items.Add(channel, $"通道 {channel}");
        return items;
    }
}
