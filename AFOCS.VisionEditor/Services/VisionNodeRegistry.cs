using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.VisionEditor.Models;

namespace AFOCS.VisionEditor.Services
{
    /// <summary>
    /// 视觉节点注册表 —— 只包含视觉节点，供视觉编辑器使用。
    /// </summary>
    public interface IVisionNodeRegistry : INodeRegistry
    {
    }

    [Export(typeof(IVisionNodeRegistry))]
    public class VisionNodeRegistry : NodeRegistry, IVisionNodeRegistry
    {
        [ImportingConstructor]
        public VisionNodeRegistry(
            [ImportMany(typeof(IVisionNodeDefinition))]
            IEnumerable<INodeDefinition> definitions)
            : base(definitions)
        {
        }
    }
}
