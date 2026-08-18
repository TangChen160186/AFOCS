using AFOCS.Infrastructure;
using AFOCS.VisionEditor.Services;

namespace AFOCS.App.Services;

/// <summary>
/// 视觉检测结果消息 —— 视觉检测节点执行完成后通过 IEventAggregator 发布，
/// 对应相机的监控面板（CameraTool）订阅后，在实时图像上叠加绘制 NCC 轮廓、找边线、找点十字。
/// </summary>
public class VisionInspectionMessage
{
    /// <summary>目标相机描述名（如"左上相机"），UI 据此路由到对应相机面板</summary>
    public string CameraName { get; init; } = string.Empty;

    /// <summary>当前工位（左/右），信息性字段</summary>
    public WorkPos WorkPos { get; init; }

    /// <summary>检测结果（含各步骤坐标，用于绘制）</summary>
    public VisionInspectionResult Result { get; init; } = new();

    /// <summary>NCC 模板 .shm 文件路径（用于绘制匹配轮廓），仅 Ncc 流程启用时有效</summary>
    public string ModelPath { get; init; } = string.Empty;
}
