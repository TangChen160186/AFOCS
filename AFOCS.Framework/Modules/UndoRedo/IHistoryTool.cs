using AFOCS.Framework.Framework;

namespace AFOCS.Framework.Modules.UndoRedo
{
    public interface IHistoryTool : ITool
    {
        IUndoRedoManager UndoRedoManager { get; set; }
    }
}