using AFOCS.Framework.Modules.MainMenu;
using AFOCS.Framework.Modules.StatusBar;
using AFOCS.Framework.Modules.ToolBars;
using Caliburn.Micro;

namespace AFOCS.Framework.Framework.Services
{
    public interface IShell : IGuardClose, IDeactivate
	{
        event EventHandler ActiveDocumentChanging;
        event EventHandler ActiveDocumentChanged;

        bool ShowFloatingWindowsInTaskbar { get; set; }
        
		IMenu MainMenu { get; }
        IToolBars ToolBars { get; }
		IStatusBar StatusBar { get; }

        // TODO: Rename this to ActiveItem.
        ILayoutItem ActiveLayoutItem { get; set; }

        // TODO: Rename this to SelectedDocument.
		IDocument ActiveItem { get; }

		IObservableCollection<IDocument> Documents { get; }
		IObservableCollection<ITool> Tools { get; }

        bool RegisterTool(ITool tool);
        void ShowTool<TTool>() where TTool : ITool;
		void ShowTool(ITool model);

		Task OpenDocumentAsync(IDocument model);
		Task CloseDocumentAsync(IDocument document);

		void Close();
	}
}
