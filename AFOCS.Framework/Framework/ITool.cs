using AFOCS.Framework.Framework.Services;

namespace AFOCS.Framework.Framework
{
	public interface ITool : ILayoutItem
	{
		PaneLocation PreferredLocation { get; }
        double PreferredWidth { get; }
        double PreferredHeight { get; }

		bool IsVisible { get; set; }
	}
}