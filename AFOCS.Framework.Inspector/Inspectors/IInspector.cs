namespace AFOCS.Framework.Inspector.Inspectors
{
    public interface IInspector
    {
        string Name { get; }
        bool IsReadOnly { get; }
    }
}