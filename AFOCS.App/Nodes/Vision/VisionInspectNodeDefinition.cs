using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.VisionEditor.Models;
using AFOCS.VisionEditor.Services;
using Emgu.CV;
using Serilog;

namespace AFOCS.App.Nodes.Vision;

/// <summary>
/// 视觉检测节点：根据模板对新图进行视觉检测，输出 NCC 中心偏移、找边角度偏差、找点位置偏差。
/// 模板中已启用的流程若执行失败，整个节点报错。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.VisionInspect", "视觉检测", "视觉")]
[method: ImportingConstructor]
public class VisionInspectNodeDefinition(ILogger logger) : NodeDefinitionBase, IExecutableNode
{
    // ========== 输入端口 ==========

    /// <summary>输入图像（Mat 灰度图）</summary>
    [NodePort("Image", "图像", NodePortType.Mat, true)]
    public Mat? Image { get; set; }

    // ========== 输出端口 ==========

    [ReadOnly(true)]
    [NodePort("NccDx", "NCC ΔX", NodePortType.Double, false)]
    public double NccDx { get; set; }

    [ReadOnly(true)]
    [NodePort("NccDy", "NCC ΔY", NodePortType.Double, false)]
    public double NccDy { get; set; }

    [ReadOnly(true)]
    [NodePort("Edge1AngleDev", "边1角度偏差", NodePortType.Double, false)]
    public double Edge1AngleDev { get; set; }

    [ReadOnly(true)]
    [NodePort("Edge2AngleDev", "边2角度偏差", NodePortType.Double, false)]
    public double Edge2AngleDev { get; set; }

    [ReadOnly(true)]
    [NodePort("PointDevX", "交点ΔX", NodePortType.Double, false)]
    public double PointDevX { get; set; }

    [ReadOnly(true)]
    [NodePort("PointDevY", "交点ΔY", NodePortType.Double, false)]
    public double PointDevY { get; set; }

    // ========== 配置属性 ==========

    [DisplayName("模板路径")]
    [Editor(typeof(VtemplateFileEditor), typeof(VtemplateFileEditor))]
    public string TemplatePath
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    // ========== 执行 ==========

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        // 校验模板路径
        if (string.IsNullOrWhiteSpace(TemplatePath))
            throw new InvalidOperationException("视觉检测节点：模板路径为空，请先选择 .vtemplate 文件");
        if (!File.Exists(TemplatePath))
            throw new InvalidOperationException($"视觉检测节点：模板文件不存在 \"{TemplatePath}\"");

        // 获取输入图像
        var grayImage = Image;
        if (grayImage == null || grayImage.IsEmpty)
            throw new InvalidOperationException("视觉检测节点：输入图像为空，请连接相机节点");

        // 加载模板
        var json = File.ReadAllText(TemplatePath);
        var template = JsonHelper.Deserialize<VisionTemplate>(json);
        if (template == null)
            throw new InvalidOperationException($"视觉检测节点：模板解析失败 \"{TemplatePath}\"");

        // 执行检测
        VisionInspectionResult result;
        try
        {
            var service = new VisionInspectionService();
            result = service.Inspect(grayImage, null, template)
                ?? throw new InvalidOperationException("视觉检测节点：Inspect 返回 null");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "视觉检测执行失败");
            throw;
        }

        // 检查已启用的流程是否全部成功，任一失败则整个节点报错
        var errors = new List<string>(4);
        if (template.Ncc.IsEnabled && !result.NccSuccess)
            errors.Add("NCC 模板匹配失败");
        if (template.EdgeFind1.IsEnabled && !result.Edge1Success)
            errors.Add("找边1 失败");
        if (template.EdgeFind2.IsEnabled && !result.Edge2Success)
            errors.Add("找边2 失败");
        if (template.PointFind.IsEnabled && !result.PointSuccess)
            errors.Add("找点 失败");

        if (errors.Count > 0)
            throw new InvalidOperationException("视觉检测节点：" + string.Join("；", errors));

        // 写回输出端口
        NccDx = result.Dx;
        NccDy = result.Dy;
        Edge1AngleDev = result.Edge1AngleDev;
        Edge2AngleDev = result.Edge2AngleDev;
        PointDevX = result.PointDevX;
        PointDevY = result.PointDevY;

        return new Dictionary<string, object?>
        {
            ["NccDx"] = result.Dx,
            ["NccDy"] = result.Dy,
            ["Edge1AngleDev"] = result.Edge1AngleDev,
            ["Edge2AngleDev"] = result.Edge2AngleDev,
            ["PointDevX"] = result.PointDevX,
            ["PointDevY"] = result.PointDevY,
        };
    }
}
