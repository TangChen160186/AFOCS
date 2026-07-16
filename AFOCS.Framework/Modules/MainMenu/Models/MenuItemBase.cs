using System.Collections;
using Caliburn.Micro;

namespace AFOCS.Framework.Modules.MainMenu.Models
{
	public class MenuItemBase : PropertyChangedBase, IEnumerable<MenuItemBase>
	{
        #region Static stuff

        public static MenuItemBase Separator => new MenuItemSeparator();

        #endregion

        #region Properties

        public IObservableCollection<MenuItemBase> Children { get; private set; }
            = new BindableCollection<MenuItemBase>();

        #endregion

        #region Constructors

        protected MenuItemBase()
		{
		}

        #endregion

        public void Add(params MenuItemBase[] menuItems)
            => menuItems.Apply(Children.Add);

        public IEnumerator<MenuItemBase> GetEnumerator()
            => Children.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}
