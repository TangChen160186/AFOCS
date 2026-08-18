using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Text.Json.Serialization;
using AFOCS.App.Services;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.VisionEditor.Models;
using AFOCS.VisionEditor.Services;
using Caliburn.Micro;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Vision;

/// <summary>
/// 视觉检测节点：根据模板对新图进行视觉检测，输出 NCC 中心偏移、找边角度偏差、找点位置偏差。
/// 模板中已启用的流程若执行失败，整个节点报错。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.VisionInspect", "视觉检测", "视觉")]
[CategoryOrder("基础", 0), 
 CategoryOrder("配置", 1), 
 CategoryOrder("输入", 2),
 CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class VisionInspectNodeDefinition(ILogger logger, IEventAggregator eventAggregator) : NodeDefinitionBase, IExecutableNode
{
    // ========== 输入端口 ==========

    /// <summary>输入图像（PixelData 灰度图，来自相机采集或图片文件节点）</summary>
    [JsonIgnore]
    [NodePort("Image", "图像", NodePortType.Object, true)]
    [Category("输入")]
    public PixelData? Image { get; set; }

    // ========== 输出端口 ==========
    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("NccDx", "NCC ΔX", NodePortType.Double, false)]
    [Category("输出")]
    public double NccDx { get; set; }

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("NccDy", "NCC ΔY", NodePortType.Double, false)]
    [Category("输出")]
    public double NccDy { get; set; }

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("Edge1AngleDev", "边1角度偏差", NodePortType.Double, false)]
    [Category("输出")]
    public double Edge1AngleDev { get; set; }

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("Edge2AngleDev", "边2角度偏差", NodePortType.Double, false)]
    [Category("输出")]
    public double Edge2AngleDev { get; set; }

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("PointDevX", "交点ΔX", NodePortType.Double, false)]
    [Category("输出")]
    public double PointDevX { get; set; }

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("PointDevY", "交点ΔY", NodePortType.Double, false)]
    [Category("输出")]
    public double PointDevY { get; set; }

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("PointX", "找点X", NodePortType.Double, false)]
    [Category("输出")]
    public double PointX { get; set; }

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("PointY", "找点Y", NodePortType.Double, false)]
    [Category("输出")]
    public double PointY { get; set; }

    // ========== 配置属性 ==========

    [DisplayName("模板路径")]
    [Editor(typeof(VtemplateFileEditor), typeof(VtemplateFileEditor))]
    [Category("配置")]
    public string TemplatePath
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    [DisplayName("相机")]
    [Description("检测结果绘制到该相机的监控面板；留空则自动跟随上游相机采集节点")]
    [ItemsSource(typeof(CameraCaptureNodeDefinition.CameraItemsSource))]
    [Category("配置")]
    public string CameraName
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    // ========== 执行 ==========

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        if (string.IsNullOrWhiteSpace(TemplatePath))
            throw new InvalidOperationException("视觉检测节点：模板路径为空，请先选择 .vtemplate 文件");
        if (!File.Exists(TemplatePath))
            throw new InvalidOperationException($"视觉检测节点：模板文件不存在 \"{TemplatePath}\"");

        var pixelData = Image;
        if (pixelData == null || pixelData.Data.Length == 0)
            throw new InvalidOperationException("视觉检测节点：输入图像为空，请连接相机节点");

        var json = await File.ReadAllTextAsync(TemplatePath);
        var template = JsonHelper.Deserialize<VisionTemplate>(json);
        if (template == null)
            throw new InvalidOperationException($"视觉检测节点：模板解析失败 \"{TemplatePath}\"");

        // PixelData → HImage → 执行检测
        VisionInspectionResult result;
        try
        {
            using var hImage = pixelData.ToHImage();
            var service = new VisionInspectionService();
            result = service.Inspect(hImage, template)
                ?? throw new InvalidOperationException("视觉检测节点：Inspect 返回 null");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "视觉检测执行失败");
            throw;
        }

        var errors = new List<string>(4);
        if (template.Ncc.IsEnabled && !result.NccSuccess) errors.Add("NCC 模板匹配失败");
        if (template.EdgeFind1.IsEnabled && !result.Edge1Success) errors.Add("找边1 失败");
        if (template.EdgeFind2.IsEnabled && !result.Edge2Success) errors.Add("找边2 失败");
        if (template.PointFind.IsEnabled && !result.PointSuccess) errors.Add("找点 失败");

        if (errors.Count > 0)
            throw new InvalidOperationException("视觉检测节点：" + string.Join("；", errors));

        NccDx = result.Dx;
        NccDy = result.Dy;
        Edge1AngleDev = result.Edge1AngleDev;
        Edge2AngleDev = result.Edge2AngleDev;
        PointDevX = result.PointDevX;
        PointDevY = result.PointDevY;
        PointX = result.PointResultX;
        PointY = result.PointResultY;

        logger.Information($"视觉检测完成,结果: NccDx={result.Dx}, NccDy={result.Dy}, Edge1AngleDev={result.Edge1AngleDev}, Edge2AngleDev={result.Edge2AngleDev}, PointDevX={result.PointDevX}, PointDevY={result.PointDevY}, PointX={result.PointResultX}, PointY={result.PointResultY}");

        PublishInspectionResult(context, result, template);

        return new Dictionary<string, object?>
        {
            ["NccDx"] = result.Dx,
            ["NccDy"] = result.Dy,
            ["Edge1AngleDev"] = result.Edge1AngleDev,
            ["Edge2AngleDev"] = result.Edge2AngleDev,
            ["PointDevX"] = result.PointDevX,
            ["PointDevY"] = result.PointDevY,
            ["PointX"] = result.PointResultX,
            ["PointY"] = result.PointResultY,
        };
    }

    /// <summary>
    /// 发布视觉检测结果消息，供对应相机的监控面板叠加绘制。
    /// 相机名解析优先级：节点配置的相机 > 上游相机采集节点写入上下文的相机。
    /// </summary>
    private void PublishInspectionResult(
        Dictionary<string, object?> context, VisionInspectionResult result, VisionTemplate template)
    {
        var cameraName = CameraName;
        if (string.IsNullOrWhiteSpace(cameraName)
            && context.TryGetValue("CameraName", out var ctxCamera) && ctxCamera is string s)
        {
            cameraName = s;
        }

        if (string.IsNullOrWhiteSpace(cameraName))
            return;

        var workPos = context.TryGetValue(FlowExecutor.WorkPosKey, out var wp) && wp is WorkPos pos
            ? pos
            : WorkPos.None;

        _ = eventAggregator.PublishOnUIThreadAsync(new VisionInspectionMessage
        {
            CameraName = cameraName,
            WorkPos = workPos,
            Result = result,
            ModelPath = template.Ncc.ModelPath ?? string.Empty,
        });
    }
}
