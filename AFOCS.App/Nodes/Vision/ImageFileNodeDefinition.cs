using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.VisionEditor.Models;
using HalconDotNet;
using Serilog.Core;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Vision;

/// <summary>
/// 图片文件节点：从磁盘加载图片并转为 PixelData 灰度图输出，用于离线测试视觉检测流程。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.ImageFile", "图片文件", "视觉")]
[CategoryOrder("基础", 0),
 CategoryOrder("配置", 1), 
 CategoryOrder("输入", 2), 
 CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class ImageFileNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    // ========== 输出端口 ==========

    [Browsable(false)]
    [NodePort("Image", "图像", NodePortType.Object, false)]
    [Category("输出")]
    public PixelData? Image { get; set; }

    // ========== 配置属性 ==========

    [DisplayName("图片路径")]
    [Editor(typeof(ImageFileEditor), typeof(ImageFileEditor))]
    [Category("配置")]
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

        using var hImage = new HImage(ImagePath);
        hImage.GetImageSize(out int w, out int h);
        int size = w * h;
        byte[] data = new byte[size];

        // 先转为灰度图，再取指针
        using var grayImage = hImage.Rgb1ToGray();
        HOperatorSet.GetImagePointer1(grayImage, out HTuple pointer, out HTuple _, out HTuple _, out HTuple _);

        unsafe
        {
            fixed (byte* pData = data)
            {
                Buffer.MemoryCopy((void*)pointer.IP, pData, size, size);
            }
        }

        Image = new PixelData(data, w, h, 1);
        return Task.FromResult(new Dictionary<string, object?> { ["Image"] = Image });
    }
}
