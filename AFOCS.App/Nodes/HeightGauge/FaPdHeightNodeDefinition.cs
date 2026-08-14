using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Text.Json.Serialization;
using AFOCS.App.Models;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.HeightGauge;

/// <summary>
/// FA 下表面到 PD 测高计算节点。
/// 读取全局标定值（P0/H0/Y0/Precision），结合运行时输入计算最终高度：
///   H_final = (P1 - P0) + H_pd - (Y1 - Y0) × Precision - H0
/// 输入：
///   AxisPos(P1)  —— 来自「示教点坐标」节点（测高方向轴）
///   PdHeight(H_pd)—— 来自「测高仪读取」节点
///   PixelY(Y1)   —— 来自「视觉检测」节点的找点Y（PointY）
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.FaPdHeight", "FA下表面PD测高", "设备")]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class FaPdHeightNodeDefinition(IConfigService configService, ILogger logger)
    : NodeDefinitionBase, IExecutableNode
{
    // ========== 输入端口 ==========

    [DisplayName("轴位置")]
    [NodePort("AxisPos", "轴位置", NodePortType.Double, true)]
    [Category("输入")]
    public double AxisPos { get; set; }

    [DisplayName("PD高度")]
    [NodePort("PdHeight", "PD高度", NodePortType.Double, true)]
    [Category("输入")]
    public double PdHeight { get; set; }

    [DisplayName("像素Y")]
    [NodePort("PixelY", "像素Y", NodePortType.Double, true)]
    [Category("输入")]
    public double PixelY { get; set; }

    // ========== 输出端口 ==========

    [JsonIgnore]
    [ReadOnly(true)]
    [NodePort("Height", "高度", NodePortType.Double, false)]
    [Category("输出")]
    public double Height { get; set; }

    // ========== 执行 ==========

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        var calib = await configService.LoadAsync<FaPdCalibrationConfig>();
        if (calib == null || !calib.IsCalibrated)
            throw new InvalidOperationException("FA下表面PD测高节点：尚未标定，请先在标定界面完成标定");

        var deltaAxis = AxisPos - calib.AxisPosition;
        var deltaPixel = (PixelY - calib.PixelY) * calib.Precision;
        var height = deltaAxis + PdHeight - deltaPixel - calib.HeightValue;

        Height = height;
        logger.Information(
            "FA下表面PD测高节点：AxisPos={AxisPos}, PdHeight={PdHeight}, PixelY={PixelY}, Height={Height}",
            AxisPos, PdHeight, PixelY, height);

        return new Dictionary<string, object?> { ["Height"] = height };
    }
}
