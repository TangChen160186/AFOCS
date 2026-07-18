using AFOCS.Framework.Inspector.Inspectors;

namespace AFOCS.Framework.Inspector
{
    public interface IInspectableObject
    {
        IEnumerable<IInspector> Inspectors { get; }
    }
}