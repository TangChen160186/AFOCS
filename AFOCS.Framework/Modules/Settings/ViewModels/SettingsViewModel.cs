using System.ComponentModel.Composition;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Properties;
using Caliburn.Micro;
using Gemini.Modules.Settings;

namespace AFOCS.Framework.Modules.Settings.ViewModels
{
    [Export(typeof (SettingsViewModel))]
    [PartCreationPolicy (CreationPolicy.NonShared)]
    public class SettingsViewModel : WindowBase
    {
        private IEnumerable<ISettingsEditorAsync> _settingsEditors;
        private SettingsPageViewModel _selectedPage;
        private bool _saved;

        public SettingsViewModel()
        {
            DisplayName = Resources.SettingsDisplayName;
        }

        public List<SettingsPageViewModel> Pages { get; internal set; }

        public SettingsPageViewModel SelectedPage
        {
            get { return _selectedPage; }
            set
            {
                _selectedPage = value;
                NotifyOfPropertyChange(() => SelectedPage);
            }
        }

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            await base.OnInitializeAsync(cancellationToken);

            var pages = new List<SettingsPageViewModel>();

            _settingsEditors = IoC.GetAll<ISettingsEditorAsync>().Concat(IoC.GetAll<ISettingsEditor>().Select(e => new SettingsEditorWrapper(e)));

            foreach (var settingsEditor in _settingsEditors)
            {
                var parentCollection = GetParentCollection(settingsEditor, pages);

                var page = parentCollection.FirstOrDefault(m => m.Name == settingsEditor.SettingsPageName);

                if (page == null)
                {
                    page = new SettingsPageViewModel { Name = settingsEditor.SettingsPageName };
                    parentCollection.Add(page);
                }

                page.Editors.Add(settingsEditor is SettingsEditorWrapper wrapper ? (object)wrapper.ViewModel : (object)settingsEditor);
            }

            Pages = pages;
            SelectedPage = GetFirstLeafPageRecursive(pages);
        }

        private static SettingsPageViewModel GetFirstLeafPageRecursive(List<SettingsPageViewModel> pages)
        {
            if (!pages.Any())
                return null;

            var firstPage = pages.First();
            if (!firstPage.Children.Any())
                return firstPage;

            return GetFirstLeafPageRecursive(firstPage.Children);
        }

        private List<SettingsPageViewModel> GetParentCollection(ISettingsEditorAsync settingsEditor,
            List<SettingsPageViewModel> pages)
        {
            if (string.IsNullOrEmpty(settingsEditor.SettingsPagePath))
            {
                return pages;
            }

            var path = settingsEditor.SettingsPagePath.Split(new[] {'\\'}, StringSplitOptions.RemoveEmptyEntries);

            foreach (var pathElement in path)
            {
                var page = pages.FirstOrDefault(s => s.Name == pathElement);

                if (page == null)
                {
                    page = new SettingsPageViewModel { Name = pathElement };
                    pages.Add(page);
                }

                pages = page.Children;
            }

            return pages;
        }

        public async Task SaveChanges()
        {
            foreach (var settingsEditor in _settingsEditors)
            {
                await settingsEditor.ApplyChangesAsync();
            }

            _saved = true;
            await TryCloseAsync(true);
        }

        public Task Cancel() => TryCloseAsync(false);

        public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken)
        {
            if (_saved) return true;

            foreach (var editor in _settingsEditors)
            {
                if (editor is SettingsEditorWrapper wrapper && wrapper.ViewModel is ICancelableSettingsEditor c1)
                    c1.CancelChanges();
                else if (editor is ICancelableSettingsEditor c2)
                    c2.CancelChanges();
            }

            return true;
        }
    }
}
