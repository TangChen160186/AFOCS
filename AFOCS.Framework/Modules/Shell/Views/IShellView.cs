using System.IO;
using AFOCS.Framework.Framework;

namespace AFOCS.Framework.Modules.Shell.Views
{
    public interface IShellView
    {
        void LoadLayout(Stream stream, Action<ITool> addToolCallback, Action<IDocument> addDocumentCallback,
                        Dictionary<string, ILayoutItem> itemsState);

        void SaveLayout(Stream stream);

        void UpdateFloatingWindows();
    }
}