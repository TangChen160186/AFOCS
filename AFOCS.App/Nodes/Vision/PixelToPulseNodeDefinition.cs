using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Devices.Camera;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure.Extensions;
using Serilog;
using Serilog.Core;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Vision;

/// <summary>
/// 像素→脉冲转换节点：将视觉检测的像素偏差转为耦合轴脉冲值。
/// 选择相机后自动读取其精度(um/pixel)，脉冲 = 像素 × 精度 × 204.8。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.PixelToPulse", "像素→脉冲", "视觉")]
[CategoryOrder("基础", 0), 
 CategoryOrder("配置", 1),
 CategoryOrder("输入", 2),
 CategoryOrder("输出", 3)]
public class PixelToPulseNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    // ========== 输入端口 ==========

    [NodePort("PixelValue", "像素值", NodePortType.Double, true)]
    [Category("输入")]
    public double PixelValue { get; set; }

    // ========== 输出端口 ==========

    [Browsable(false)]
    [NodePort("PulseValue", "脉冲值", NodePortType.Double, false)]
    [Category("输出")]
    [ReadOnly(true)]
    public double PulseValue { get; set; }

    // ========== 配置属性 ==========

    [DisplayName("相机")]
    [ItemsSource(typeof(CameraItemsSource))]
    [Category("配置")]
    public string CameraName
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    private const double PulsesPerUm = 204.8;


    private readonly Dictionary<string, ICamera> _cameraMap;

    private static IEnumerable<string> _cameraNames = null!;

    [method: ImportingConstructor]
    public PixelToPulseNodeDefinition([ImportMany] IEnumerable<ICamera> cameras)
    {
        _cameraNames = cameras.Select(c => c.GetType().GetDescription());
        _cameraMap = cameras
            .ToDictionary(c => c.GetType().GetDescription());
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        if (string.IsNullOrWhiteSpace(CameraName))
            throw new InvalidOperationException("相机采集节点：未选择相机");
        if (!_cameraMap.TryGetValue(CameraName, out var camera))
            throw new InvalidOperationException($"相机采集节点：未找到相机 \"{CameraName}\"，可用：{string.Join(", ", _cameraMap.Keys)}");

        double precision = camera.GetConfig().Precision;
        PulseValue = PixelValue * precision * PulsesPerUm;
        return Task.FromResult(new Dictionary<string, object?> { ["PulseValue"] = PulseValue });
    }

    /// <summary>相机列表下拉源</summary>
    public class CameraItemsSource : IItemsSource
    {
        public ItemCollection GetValues()
        {
            var items = new ItemCollection();

            foreach (var name in _cameraNames)
                items.Add(name, name);
            return items;
        }
    }
}
