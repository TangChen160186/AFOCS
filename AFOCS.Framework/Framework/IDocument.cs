using AFOCS.Framework.Modules.UndoRedo;

namespace AFOCS.Framework.Framework
{
	public interface IDocument : ILayoutItem
	{
        IUndoRedoManager UndoRedoManager { get; }
	}
}