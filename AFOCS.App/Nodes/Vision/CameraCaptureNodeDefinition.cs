using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using AFOCS.Devices.Camera;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Vision;

/// <summary>
/// 相机采集节点：从指定相机获取一帧灰度图，输出 Mat 给下游视觉检测节点。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.CameraCapture", "相机采集", "视觉")]
[method: ImportingConstructor]
public class CameraCaptureNodeDefinition(
    ILogger logger,
    [ImportMany] IEnumerable<ICamera> cameras) : NodeDefinitionBase, IExecutableNode
{
    // ========== 输出端口 ==========

    [Browsable(false)]
    [NodePort("Image", "图像", NodePortType.Mat, false)]
    public Mat? Image { get; set; }

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

    // ========== 执行 ==========

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        // 校验相机选择
        if (string.IsNullOrWhiteSpace(CameraName))
            throw new InvalidOperationException("相机采集节点：未选择相机");
        if (!_cameraMap.TryGetValue(CameraName, out var camera))
            throw new InvalidOperationException($"相机采集节点：未找到相机 \"{CameraName}\"，可用：{string.Join(", ", _cameraMap.Keys)}");

        // 采集一帧
        var result = await camera.GrabFrameAsync();
        if (!result.IsSuccess)
            throw new InvalidOperationException($"相机采集节点：{CameraName} 采集失败 - {result.Message}");

        var (data, w, h, isMono) = result.Data;

        // byte[] → Mat（灰度）
        Image?.Dispose();
        if (isMono)
        {
            Image = new Mat(h, w, DepthType.Cv8U, 1);
            Marshal.Copy(data, 0, Image.DataPointer, data.Length);
        }
        else
        {
            // 彩色相机：先构建 BGR Mat，再转灰度
            using var bgr = new Mat(h, w, DepthType.Cv8U, 3);
            Marshal.Copy(data, 0, bgr.DataPointer, data.Length);
            Image = new Mat();
            CvInvoke.CvtColor(bgr, Image, ColorConversion.Bgr2Gray);
        }

        return new Dictionary<string, object?> { ["Image"] = Image };
    }

    /// <summary>相机列表下拉源</summary>
    public class CameraItemsSource : IItemsSource
    {
        public ItemCollection GetValues()
        {
            var items = new ItemCollection();
            // 四个相机名固定，与 Cameras.cs 中注册一致
            foreach (var name in new[] { "左上相机", "左下相机", "右上相机", "右下相机" })
                items.Add(name, name);
            return items;
        }
    }
}
