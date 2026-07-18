using System.ComponentModel;
using AFOCS.Framework.Inspector.Inspectors;

namespace AFOCS.Framework.Inspector.Conventions
{
    public abstract class PropertyEditorBuilder
    {
        public abstract bool IsApplicable(PropertyDescriptor propertyDescriptor);
        public abstract IEditor BuildEditor(PropertyDescriptor propertyDescriptor);
    }
}