namespace AFOCS.Framework.Inspector.Inspectors
{
    public interface IEditor : IInspector
    {
        BoundPropertyDescriptor BoundPropertyDescriptor { get; set; }
        bool CanReset { get; }
        void Reset();
    }
}