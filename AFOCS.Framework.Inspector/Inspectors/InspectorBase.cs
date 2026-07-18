using Caliburn.Micro;

namespace AFOCS.Framework.Inspector.Inspectors
{
    public abstract class InspectorBase : PropertyChangedBase, IInspector
    {
        public abstract string Name { get; }
        public abstract bool IsReadOnly { get; }
    }
}