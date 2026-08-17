using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Text.Json.Serialization;
using AFOCS.App.Models;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Motion;

public enum RotationAxisOption
{
    [Description("X 轴")]
    X,

    [Description("Y 轴")]
    Y,

    [Description("Z 轴")]
    Z,
}

/// <summary>
/// 夹爪旋转补偿节点：夹爪旋转中心偏离自身中心，绕指定轴旋转后需补偿。
/// 由「直线轴初始角度 + 旋转半径」与旋转角度计算补偿偏移量（um）：
///   Comp = 半径 × (cos(初始角度 + 旋转角度) − cos(初始角度))
/// 绕 X 轴旋转 → 用 Y、Z 轴的配置补偿（影响 YZ 平面）
/// 绕 Y 轴旋转 → 用 X、Z 轴的配置补偿（影响 XZ 平面）
/// 绕 Z 轴旋转 → 用 X、Y 轴的配置补偿（影响 XY 平面）
/// 初始角度与半径在「设备配置 → 夹爪旋转补偿」设置页维护。
/// 说明：输出为旋转带来的位置偏移量，若要回原位需反向移动该值。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.RotationCompensation", "旋转补偿", "运动")]
[CategoryOrder("基础", 0),
 CategoryOrder("配置", 1),
 CategoryOrder("输入", 2), 
 CategoryOrder("输出", 3)] 
[method: ImportingConstructor]
public class RotationCompensationNodeDefinition(IConfigService configService, ILogger logger)
    : NodeDefinitionBase, IExecutableNode
{
    // ========== 输出端口 ==========

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("CompX", "补偿X", NodePortType.Double, false)]
    [Category("输出")]
    public double CompX { get; set; }

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("CompY", "补偿Y", NodePortType.Double, false)]
    [Category("输出")]
    public double CompY { get; set; }

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("CompZ", "补偿Z", NodePortType.Double, false)]
    [Category("输出")]
    public double CompZ { get; set; }

    // ========== 输入端口 ==========

    [DisplayName("旋转角度(度)")]
    [Description("本次绕所选轴旋转的角度（度），正值表示正方向")]
    [NodePort("RotationAngle", "旋转角度", NodePortType.Double, true)]
    [Category("输入")]
    public double RotationAngle { get; set; }

    // ========== 配置属性 ==========

    [DisplayName("旋转轴")]
    [ItemsSource(typeof(RotationAxisItemsSource))]
    [Category("配置")]
    public RotationAxisOption RotationAxis
    {
        get;
        set => Set(ref field, value);
    } = RotationAxisOption.Y;

    // ========== 执行 ==========

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        var cfg = await configService.LoadAsync<GripperRotationCompensationConfig>();
        if (cfg == null)
            throw new InvalidOperationException("旋转补偿节点：未找到旋转补偿配置，请先在设置页配置初始角度与半径");

        double thetaRad = RotationAngle * Math.PI / 180.0;
        double Comp(AxisRotationCompensation axis) =>
            axis.Radius * (Math.Cos(axis.InitialAngle * Math.PI / 180.0 + thetaRad)
                           - Math.Cos(axis.InitialAngle * Math.PI / 180.0));

        (CompX, CompY, CompZ) = RotationAxis switch
        {
            RotationAxisOption.X => (0.0, Comp(cfg.Y), Comp(cfg.Z)),
            RotationAxisOption.Y => (Comp(cfg.X), 0.0, Comp(cfg.Z)),
            RotationAxisOption.Z => (Comp(cfg.X), Comp(cfg.Y), 0.0),
            _ => (0.0, 0.0, 0.0),
        };

        logger.Information(
            "旋转补偿：绕{Axis}轴旋转{RotationAngle:F2}°，CompX={CompX:F3}um, CompY={CompY:F3}um, CompZ={CompZ:F3}um",
            RotationAxis.GetDescription(), RotationAngle, CompX, CompY, CompZ);

        return new Dictionary<string, object?>
        {
            ["CompX"] = CompX,
            ["CompY"] = CompY,
            ["CompZ"] = CompZ,
        };
    }
}

/// <summary>旋转轴选择（X/Y/Z）</summary>
public class RotationAxisItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        foreach (var axis in Enum.GetValues<RotationAxisOption>())
            items.Add(axis, axis.GetDescription());
        return items;
    }
}
