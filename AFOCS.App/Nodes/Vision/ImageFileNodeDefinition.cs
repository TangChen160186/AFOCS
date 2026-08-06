using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Vision;

/// <summary>
/// 图片文件节点：从磁盘加载图片并转为灰度 Mat 输出，用于离线测试视觉检测流程。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.ImageFile", "图片文件", "视觉")]
public class ImageFileNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    // ========== 输出端口 ==========

    [Browsable(false)]
    [NodePort("Image", "图像", NodePortType.Mat, false)]
    public Mat? Image { get; set; }

    // ========== 配置属性 ==========

    [DisplayName("图片路径")]
    [Editor(typeof(ImageFileEditor), typeof(ImageFileEditor))]
    public string ImagePath
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    // ========== 执行 ==========

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
            throw new InvalidOperationException("图片文件节点：图片路径为空");
        if (!File.Exists(ImagePath))
            throw new InvalidOperationException($"图片文件节点：文件不存在 \"{ImagePath}\"");

        Image?.Dispose();
        Image = CvInvoke.Imread(ImagePath, ImreadModes.Grayscale);
        if (Image == null || Image.IsEmpty)
            throw new InvalidOperationException($"图片文件节点：图片加载失败 \"{ImagePath}\"");

        return Task.FromResult(new Dictionary<string, object?> { ["Image"] = Image });
    }
}
