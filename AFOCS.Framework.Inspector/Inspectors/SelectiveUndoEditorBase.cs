using AFOCS.Framework.Framework.Services;
using Caliburn.Micro;

namespace AFOCS.Framework.Inspector.Inspectors
{
    /// <summary>
    /// This class is used for values that should only be updated after the
    /// user has finished editing them. The view needs to call OnBeginEdit when
    /// the user has started editing to capture the current value and call
    /// OnEndEdit to commit the old and new value to the undo / redo manager.
    /// </summary>
    /// <typeparam name="TValue">Type of the value</typeparam>
    public abstract class SelectiveUndoEditorBase<TValue> : EditorBase<TValue>, IDisposable
    {
        private object _originalValue = null;

        protected void OnBeginEdit()
        {
            IsUndoEnabled = false;
            _originalValue = RawValue;
        }

        protected void OnEndEdit()
        {
            if (_originalValue == null)
                return;

            try
            {
                var value = RawValue;
                if (!_originalValue.Equals(value))
                    IoC.Get<IShell>().ActiveItem.UndoRedoManager.ExecuteAction(
                        new ChangeObjectValueAction(BoundPropertyDescriptor, _originalValue, value, StringConverter));
            }
            finally
            {
                _originalValue = null;
                IsUndoEnabled = true;
            }
        }

        public override void Dispose()
        {
            OnEndEdit();
            base.Dispose();
        }
    }
}
