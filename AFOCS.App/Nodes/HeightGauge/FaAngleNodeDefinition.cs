using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Text.Json.Serialization;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.HeightGauge;

/// <summary>
/// FA 角度计算节点。
/// 输入两个测高结果与两个示教点之间的水平差值，输出倾斜角度（度）：
///   ΔH = Height1 - Height2
///   Angle = atan2(ΔH, Distance) × 180 / π
/// 输入：
///   Height1/Height2 —— 来自「测高仪读取」节点
///   Distance        —— 两个示教点之间的水平差值
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.FaAngle", "FA角度计算", "设备")]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class FaAngleNodeDefinition(ILogger logger) : NodeDefinitionBase, IExecutableNode
{
    // ========== 输入端口 ==========

    [DisplayName("测高值1")]
    [NodePort("Height1", "测高值1", NodePortType.Double, true)]
    [Category("输入")]
    public double Height1 { get; set; }

    [DisplayName("测高值2")]
    [NodePort("Height2", "测高值2", NodePortType.Double, true)]
    [Category("输入")]
    public double Height2 { get; set; }

    [DisplayName("示教点差值")]
    [NodePort("Distance", "示教点差值", NodePortType.Double, true)]
    [Category("输入")]
    public double Distance { get; set; }

    // ========== 输出端口 ==========

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("Angle", "角度", NodePortType.Double, false)]
    [Category("输出")]
    public double Angle { get; set; }

    // ========== 执行 ==========

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        if (Math.Abs(Distance) < 1e-9)
            throw new InvalidOperationException("FA角度计算节点：示教点差值接近 0，无法计算角度");

        var heightDiff = Height1 - Height2;
        var angleDeg = Math.Atan2(heightDiff, Distance) * 180.0 / Math.PI;

        Angle = angleDeg;
        logger.Information(
            "FA角度计算节点：Height1={Height1}, Height2={Height2}, Distance={Distance}, Angle={Angle}°",
            Height1, Height2, Distance, angleDeg);

        return Task.FromResult(new Dictionary<string, object?> { ["Angle"] = angleDeg });
    }
}
