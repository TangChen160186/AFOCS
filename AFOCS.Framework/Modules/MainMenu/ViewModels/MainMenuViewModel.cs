using System.ComponentModel.Composition;
using AFOCS.Framework.Framework.Services;
using AFOCS.Framework.Modules.MainMenu.Models;
using ExtensionMethods = AFOCS.Framework.Framework.Services.ExtensionMethods;

namespace AFOCS.Framework.Modules.MainMenu.ViewModels
{
	[Export(typeof(IMenu))]
    public class MainMenuViewModel : MenuModel, IPartImportsSatisfiedNotification
	{
        private readonly IMenuBuilder _menuBuilder;

	    private bool _autoHide;

	    private readonly SettingsPropertyChangedEventManager<Properties.Settings> _settingsEventManager =
	        new SettingsPropertyChangedEventManager<Properties.Settings>(Properties.Settings.Default);

        [ImportingConstructor]
	    public MainMenuViewModel(IMenuBuilder menuBuilder)
	    {
            _menuBuilder = menuBuilder;
            _autoHide = Properties.Settings.Default.AutoHideMainMenu;
            _settingsEventManager.AddListener(s => s.AutoHideMainMenu, value => { AutoHide = value; });
		}

	    public bool AutoHide
	    {
	        get { return _autoHide; }
	        private set
	        {
	            if (_autoHide == value)
	                return;

	            _autoHide = value;

	            NotifyOfPropertyChange(ExtensionMethods.GetPropertyName(() => AutoHide));
	        }
	    }

	    void IPartImportsSatisfiedNotification.OnImportsSatisfied()
	    {
	        _menuBuilder.BuildMenuBar(MenuDefinitions.MainMenuBar, this);
	    }
	}
}