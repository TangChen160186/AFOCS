using System.ComponentModel.Composition;
using System.IO;
using System.Windows;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using AFOCS.Framework.Framework.Themes;
using AFOCS.Framework.Modules.MainMenu;
using AFOCS.Framework.Modules.Shell.Services;
using AFOCS.Framework.Modules.Shell.Views;
using AFOCS.Framework.Modules.StatusBar;
using AFOCS.Framework.Modules.ToolBars;
using Caliburn.Micro;

namespace AFOCS.Framework.Modules.Shell.ViewModels
{
    [Export(typeof(IShell))]
    public class ShellViewModel : Conductor<IDocument>.Collection.OneActive, IShell
    {
        public event EventHandler ActiveDocumentChanging;
        public event EventHandler ActiveDocumentChanged;

#pragma warning disable 649
        [ImportMany(typeof(IModule))]
        private IEnumerable<IModule> _modules;

        [Import]
        private IMenu _mainMenu;

        [Import]
        private IToolBars _toolBars;

        [Import]
        private IStatusBar _statusBar;

        [Import]
        private ILayoutItemStatePersister _layoutItemStatePersister;
#pragma warning restore 649

        private IShellView _shellView;
        private bool _closing;

        public IMenu MainMenu => _mainMenu;

        public IToolBars ToolBars => _toolBars;

        public IStatusBar StatusBar => _statusBar;

        private ILayoutItem _activeLayoutItem;
        public ILayoutItem ActiveLayoutItem
        {
            get => _activeLayoutItem;
            set
            {
                if (ReferenceEquals(_activeLayoutItem, value))
                    return;

                _activeLayoutItem = value;

                if (value is IDocument)
                    ActivateItemAsync((IDocument)value, CancellationToken.None).Wait();

                NotifyOfPropertyChange(() => ActiveLayoutItem);
            }
        }

        private readonly BindableCollection<ITool> _tools;
        public IObservableCollection<ITool> Tools => _tools;

        public IObservableCollection<IDocument> Documents => Items;

        private bool _showFloatingWindowsInTaskbar;
        public bool ShowFloatingWindowsInTaskbar
        {
            get => _showFloatingWindowsInTaskbar;
            set
            {
                _showFloatingWindowsInTaskbar = value;
                NotifyOfPropertyChange(() => ShowFloatingWindowsInTaskbar);
                if (_shellView != null)
                    _shellView.UpdateFloatingWindows();
            }
        }

        public virtual string StateFile => @".\ApplicationState.bin";

        public bool HasPersistedState => File.Exists(StateFile);

        public ShellViewModel()
        {
            ((IActivate)this).ActivateAsync(CancellationToken.None).Wait();

            _tools = new BindableCollection<ITool>();
        }

        protected override void OnViewLoaded(object view)
        {
            foreach (var module in _modules)
                foreach (var globalResourceDictionary in module.GlobalResourceDictionaries)
                    Application.Current.Resources.MergedDictionaries.Add(globalResourceDictionary);

            foreach (var module in _modules)
                module.PreInitialize();
            foreach (var module in _modules)
                module.Initialize();

          

            _shellView = (IShellView)view;

            Execute.OnUIThreadAsync(async () =>
            {
                if (!_layoutItemStatePersister.LoadState(this, _shellView, StateFile))
                {
                    foreach (var defaultDocument in _modules.SelectMany(x => x.DefaultDocuments))
                        await OpenDocumentAsync(defaultDocument);
                    foreach (var defaultTool in _modules.SelectMany(x => x.DefaultTools))
                        ShowTool((ITool)IoC.GetInstance(defaultTool, null));
                }

                foreach (var module in _modules)
                    await module.PostInitializeAsync();
            });

            base.OnViewLoaded(view);
        }

        public bool RegisterTool(ITool tool)
        {
            if (Tools.Contains(tool))
                return false;

            Tools.Add(tool);
            return true;
        }

        public void ShowTool<TTool>()
            where TTool : ITool
        {
            ShowTool(IoC.Get<TTool>());
        }

        public void ShowTool(ITool model)
        {
            #region debug-point D/E:show-tool
            try
            {
                var dbgJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    sessionId = "camera-tool-open-fail",
                    runId = "pre",
                    hypothesisId = "D",
                    location = "ShellViewModel.ShowTool",
                    msg = "[DEBUG] ShowTool 被调用",
                    data = new { toolType = model.GetType().FullName, isActive = model.IsActive, isVisible = model.IsVisible },
                });
                _ = new System.Net.Http.HttpClient().PostAsync("http://127.0.0.1:7777/event", new System.Net.Http.StringContent(dbgJson, System.Text.Encoding.UTF8, "application/json"));
            }
            catch { }
            #endregion

            RegisterTool(model);

            if (!model.IsActive)
            {
                #region debug-point D/E:activate
                try
                {
                    model.ActivateAsync(CancellationToken.None).Wait();
                }
                catch (Exception ex)
                {
                    try
                    {
                        var dbgJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            sessionId = "camera-tool-open-fail",
                            runId = "pre",
                            hypothesisId = "D",
                            location = "ShellViewModel.ShowTool",
                            msg = "[DEBUG] ActivateAsync 异常",
                            data = new { toolType = model.GetType().FullName, error = ex.ToString() },
                        });
                        _ = new System.Net.Http.HttpClient().PostAsync("http://127.0.0.1:7777/event", new System.Net.Http.StringContent(dbgJson, System.Text.Encoding.UTF8, "application/json"));
                    }
                    catch { }
                    throw;
                }
                #endregion
            }

            model.IsVisible = true;
            model.IsSelected = true;
            ActiveLayoutItem = model;

            #region debug-point E:after-show
            try
            {
                var dbgJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    sessionId = "camera-tool-open-fail",
                    runId = "pre",
                    hypothesisId = "E",
                    location = "ShellViewModel.ShowTool",
                    msg = "[DEBUG] ShowTool 完成",
                    data = new { toolType = model.GetType().FullName, isActive = model.IsActive, isVisible = model.IsVisible, activeLayout = ActiveLayoutItem?.GetType().FullName },
                });
                _ = new System.Net.Http.HttpClient().PostAsync("http://127.0.0.1:7777/event", new System.Net.Http.StringContent(dbgJson, System.Text.Encoding.UTF8, "application/json"));
            }
            catch { }
            #endregion
        }

        public Task OpenDocumentAsync(IDocument model) => ActivateItemAsync(model, CancellationToken.None);

        public Task CloseDocumentAsync(IDocument document) => DeactivateItemAsync(document, true, CancellationToken.None);

        private bool _activateItemGuard = false;

        public override async Task ActivateItemAsync(IDocument item, CancellationToken cancellationToken)
        {
            if (_closing || _activateItemGuard)
                return;

            _activateItemGuard = true;

            try
            {
                if (ReferenceEquals(item, ActiveItem))
                    return;

                RaiseActiveDocumentChanging();

                var currentActiveItem = ActiveItem;

                await base.ActivateItemAsync(item, cancellationToken);

                RaiseActiveDocumentChanged();
            }
            finally
            {
                _activateItemGuard = false;
            }
        }

        private void RaiseActiveDocumentChanging()
        {
            var handler = ActiveDocumentChanging;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void RaiseActiveDocumentChanged()
        {
            var handler = ActiveDocumentChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        protected override void OnActivationProcessed(IDocument item, bool success)
        {
            if (!ReferenceEquals(ActiveLayoutItem, item))
                ActiveLayoutItem = item;

            base.OnActivationProcessed(item, success);
        }

        public override async Task DeactivateItemAsync(IDocument item, bool close, CancellationToken cancellationToken)
        {
            RaiseActiveDocumentChanging();

            await base.DeactivateItemAsync(item, close, cancellationToken);

            RaiseActiveDocumentChanged();
        }

        protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            // Workaround for a complex bug that occurs when
            // (a) the window has multiple documents open, and
            // (b) the last document is NOT active
            // 
            // The issue manifests itself with a crash in
            // the call to base.ActivateItem(item), above,
            // saying that the collection can't be changed
            // in a CollectionChanged event handler.
            // 
            // The issue occurs because:
            // - Caliburn.Micro sees the window is closing, and calls Items.Clear()
            // - AvalonDock handles the CollectionChanged event, and calls Remove()
            //   on each of the open documents.
            // - If removing a document causes another to become active, then AvalonDock
            //   sets a new ActiveContent.
            // - We have a WPF binding from Caliburn.Micro's ActiveItem to AvalonDock's
            //   ActiveContent property, so ActiveItem gets updated.
            // - The document no longer exists in Items, beacuse that collection was cleared,
            //   but Caliburn.Micro helpfully adds it again - which causes the crash.
            //
            // My workaround is to use the following _closing variable, and ignore activation
            // requests that occur when _closing is true.
            _closing = true;

            _layoutItemStatePersister.SaveState(this, _shellView, StateFile);

            await base.OnDeactivateAsync(close, cancellationToken);
        }

        public void Close()
        {
            Application.Current.MainWindow.Close();
        }
    }
}
