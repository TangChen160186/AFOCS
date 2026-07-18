using AFOCS.Framework.Framework;

namespace AFOCS.Framework.Inspector
{
	public interface IInspectorTool : ITool
	{
	    event EventHandler SelectedObjectChanged;
        IInspectableObject SelectedObject { get; set; }
	}
}