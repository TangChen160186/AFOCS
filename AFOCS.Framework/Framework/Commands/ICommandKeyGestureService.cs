using System.Windows;
using System.Windows.Input;

namespace AFOCS.Framework.Framework.Commands
{
    public interface ICommandKeyGestureService
    {
        void BindKeyGestures(UIElement uiElement);
        KeyGesture GetPrimaryKeyGesture(CommandDefinitionBase commandDefinition);
    }
}