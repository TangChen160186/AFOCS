using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.VisionEditor.Models
{
    /// <summary>
    /// 视觉节点标记接口。
    /// 视觉节点通过 [Export(typeof(IVisionNodeDefinition))] 导出，
    /// 与流程节点（[Export(typeof(INodeDefinition))]）互不干扰，
    /// 这样流程编辑器工具箱不会出现视觉节点。
    /// </summary>
    public interface IVisionNodeDefinition : INodeDefinition
    {
    }
}
