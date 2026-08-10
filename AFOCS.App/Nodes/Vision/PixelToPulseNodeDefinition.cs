using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Devices.Camera;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Vision;

/// <summary>
/// 像素→脉冲转换节点：将视觉检测的像素偏差转为耦合轴脉冲值。
/// 选择相机后自动读取其精度(mm/pixel)，脉冲 = 像素 × 精度 × 204800。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.PixelToPulse", "像素→脉冲", "视觉")]
[method: ImportingConstructor]
public class PixelToPulseNodeDefinition(
    [ImportMany] IEnumerable<ICamera> cameras) : NodeDefinitionBase, IExecutableNode
{
    // ========== 输入端口 ==========

    [NodePort("PixelValue", "像素值", NodePortType.Double, true)]
    public double PixelValue { get; set; }

    // ========== 输出端口 ==========

    [Browsable(false)]
    [NodePort("PulseValue", "脉冲值", NodePortType.Double, false)]
    public double PulseValue { get; set; }

    // ========== 配置属性 ==========

    private readonly Dictionary<string, ICamera> _cameraMap = cameras
        .ToDictionary(c => c.GetType().Name);

    [DisplayName("相机")]
    [ItemsSource(typeof(CameraItemsSource))]
    public string CameraName
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    [DisplayName("脉冲数/mm")]
    public double PulsesPerMm
    {
        get;
        set => Set(ref field, value);
    } = 204800;

    // ========== 执行 ==========

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        // 每次执行时实时读取相机当前精度
        double precision = 0;
        if (!string.IsNullOrEmpty(CameraName) && _cameraMap.TryGetValue(CameraName, out var camera))
            precision = camera.GetConfig().Precision;

        PulseValue = PixelValue * precision * PulsesPerMm;
        return Task.FromResult(new Dictionary<string, object?> { ["PulseValue"] = PulseValue });
    }

    /// <summary>相机列表下拉源</summary>
    public class CameraItemsSource : IItemsSource
    {
        public ItemCollection GetValues()
        {
            var items = new ItemCollection();
            foreach (var name in new[] { "左上相机", "左下相机", "右上相机", "右下相机" })
                items.Add(name, name);
            return items;
        }
    }
}
