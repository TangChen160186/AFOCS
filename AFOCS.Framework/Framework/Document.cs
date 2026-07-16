using System.IO;
using System.Windows.Input;
using AFOCS.Framework.Framework.Commands;
using AFOCS.Framework.Framework.Services;
using AFOCS.Framework.Framework.Threading;
using AFOCS.Framework.Framework.ToolBars;
using AFOCS.Framework.Modules.Shell.Commands;
using AFOCS.Framework.Modules.ToolBars;
using AFOCS.Framework.Modules.ToolBars.Models;
using AFOCS.Framework.Modules.UndoRedo;
using AFOCS.Framework.Modules.UndoRedo.Commands;
using AFOCS.Framework.Modules.UndoRedo.Services;
using Caliburn.Micro;
using Microsoft.Win32;

namespace AFOCS.Framework.Framework
{
	public abstract class Document : LayoutItemBase, IDocument, 
        ICommandHandler<UndoCommandDefinition>,
        ICommandHandler<RedoCommandDefinition>,
        ICommandHandler<SaveFileCommandDefinition>,
        ICommandHandler<SaveFileAsCommandDefinition>
	{
	    private IUndoRedoManager _undoRedoManager;
	    public IUndoRedoManager UndoRedoManager
	    {
            get { return _undoRedoManager ?? (_undoRedoManager = CreateUndoRedoManager()); }
	    }

        protected virtual IUndoRedoManager CreateUndoRedoManager() => new UndoRedoManager();

        private ICommand _closeCommand;
		public override ICommand CloseCommand
		{
		    get { return _closeCommand ?? (_closeCommand = new AsyncCommand(() => TryCloseAsync(null))); }
		}

        private ToolBarDefinition _toolBarDefinition;
        public ToolBarDefinition ToolBarDefinition
        {
            get { return _toolBarDefinition; }
            protected set
            {
                _toolBarDefinition = value;
                NotifyOfPropertyChange(() => ToolBar);
                NotifyOfPropertyChange();
            }
        }

        private IToolBar _toolBar;
        public IToolBar ToolBar
        {
            get
            {
                if (_toolBar != null)
                    return _toolBar;

                if (ToolBarDefinition == null)
                    return null;

                var toolBarBuilder = IoC.Get<IToolBarBuilder>();
                _toolBar = new ToolBarModel();
                toolBarBuilder.BuildToolBar(ToolBarDefinition, _toolBar);
                return _toolBar;
            }
        }
        
        void ICommandHandler<UndoCommandDefinition>.Update(Command command)
	    {
            command.Enabled = UndoRedoManager.CanUndo;
	    }

	    Task ICommandHandler<UndoCommandDefinition>.Run(Command command)
	    {
            UndoRedoManager.Undo(1);
            return TaskUtility.Completed;
	    }

        void ICommandHandler<RedoCommandDefinition>.Update(Command command)
        {
            command.Enabled = UndoRedoManager.CanRedo;
        }

        Task ICommandHandler<RedoCommandDefinition>.Run(Command command)
        {
            UndoRedoManager.Redo(1);
            return TaskUtility.Completed;
        }

        void ICommandHandler<SaveFileCommandDefinition>.Update(Command command)
        {
            command.Enabled = this is IPersistedDocument;
        }

	    async Task ICommandHandler<SaveFileCommandDefinition>.Run(Command command)
	    {
	        var persistedDocument = this as IPersistedDocument;
	        if (persistedDocument == null)
	            return;

	        // If file has never been saved, show Save As dialog.
	        if (persistedDocument.IsNew)
	        {
	            await DoSaveAs(persistedDocument);
	            return;
	        }

	        // Save file.
            var filePath = persistedDocument.FilePath;
            await persistedDocument.Save(filePath);
	    }

        void ICommandHandler<SaveFileAsCommandDefinition>.Update(Command command)
        {
            command.Enabled = this is IPersistedDocument;
        }

	    async Task ICommandHandler<SaveFileAsCommandDefinition>.Run(Command command)
	    {
            var persistedDocument = this as IPersistedDocument;
            if (persistedDocument == null)
                return;

            await DoSaveAs(persistedDocument);
	    }

	    private static async Task DoSaveAs(IPersistedDocument persistedDocument)
	    {
            // Show user dialog to choose filename.
            var dialog = new SaveFileDialog();
            dialog.FileName = persistedDocument.FileName;
            var filter = string.Empty;

            var fileExtension = Path.GetExtension(persistedDocument.FileName);
            var fileType = IoC.GetAll<IEditorProvider>()
                .SelectMany(x => x.FileTypes)
                .SingleOrDefault(x => x.FileExtension == fileExtension);
            if (fileType != null)
                filter = fileType.Name + "|*" + fileType.FileExtension + "|";

            filter += "All Files|*.*";
            dialog.Filter = filter;

            if (dialog.ShowDialog() != true)
                return;

            var filePath = dialog.FileName;

            // Save file.
            await persistedDocument.Save(filePath);
	    }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            if (close)
                _undoRedoManager?.Dispose();

            return base.OnDeactivateAsync(close, cancellationToken);
        }
    }
}
