using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Devices.Camera;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure.Extensions;
using AFOCS.VisionEditor.Models;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Vision;

/// <summary>
/// 相机采集节点：从指定相机获取一帧灰度图，输出 PixelData 给下游视觉检测节点。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.CameraCapture", "相机采集", "视觉")]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
public class CameraCaptureNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    /// <summary>
    /// 所有已注册相机的描述名称。
    /// PropertyGrid 通过无参构造创建 ItemsSource，故用静态字段共享 MEF 注入的相机列表。
    /// </summary>
    private static string[] _cameraNames = [];

    private readonly Dictionary<string, ICamera> _cameraMap;

    [ImportingConstructor]
    public CameraCaptureNodeDefinition(
        ILogger logger,
        [ImportMany] IEnumerable<ICamera> cameras) : base()
    {
        _cameraNames = cameras.Select(c => c.GetType().GetDescription()).ToArray();
        _cameraMap = cameras.ToDictionary(c => c.GetType().GetDescription());
    }

    // ========== 输出端口 ==========

    [Browsable(false)]
    [NodePort("Image", "图像", NodePortType.Object, false)]
    [Category("输出")]
    public PixelData? Image { get; set; }

    // ========== 配置属性 ==========

    [DisplayName("相机")]
    [ItemsSource(typeof(CameraItemsSource))]
    [Category("配置")]
    public string CameraName
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    // ========== 执行 ==========

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        if (string.IsNullOrWhiteSpace(CameraName))
            throw new InvalidOperationException("相机采集节点：未选择相机");
        if (!_cameraMap.TryGetValue(CameraName, out var camera))
            throw new InvalidOperationException($"相机采集节点：未找到相机 \"{CameraName}\"，可用：{string.Join(", ", _cameraMap.Keys)}");

        var result = await camera.GrabFrameAsync();
        if (!result.IsSuccess)
            throw new InvalidOperationException($"相机采集节点：{CameraName} 采集失败 - {result.Message}");

        var (data, w, h, isMono) = result.Data;

        byte[] pixelData;
        int channels;
        if (isMono)
        {
            pixelData = data;
            channels = 1;
        }
        else
        {
            // 彩色相机：BGR → 灰度（简单平均）
            channels = 1;
            int totalPixels = w * h;
            pixelData = new byte[totalPixels];
            for (int i = 0; i < totalPixels; i++)
            {
                int srcIdx = i * 3;
                byte b = data[srcIdx];
                byte g = data[srcIdx + 1];
                byte r = data[srcIdx + 2];
                pixelData[i] = (byte)((r * 76 + g * 150 + b * 30) >> 8); // 加权灰度
            }
        }

        Image = new PixelData(pixelData, w, h, channels);
        return new Dictionary<string, object?> { ["Image"] = Image };
    }

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
