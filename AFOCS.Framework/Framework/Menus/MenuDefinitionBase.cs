using System.Windows.Input;
using AFOCS.Framework.Framework.Commands;

namespace AFOCS.Framework.Framework.Menus
{
    [System.Diagnostics.DebuggerDisplay("{Text}")]
    public abstract class MenuDefinitionBase
    {
        public abstract int SortOrder { get; }
        public abstract string Text { get; }
        public abstract Uri IconSource { get; }
        public abstract KeyGesture KeyGesture { get; }
        public abstract CommandDefinitionBase CommandDefinition { get; }

        /// <summary>
        /// An optional predicate which is called using this instance,
        /// which when it returns true, informs that the menu should be
        /// excluded from view
        /// </summary>
        public Predicate<MenuDefinitionBase> DynamicExclusionPredicate { get; protected set; }
    }
}
