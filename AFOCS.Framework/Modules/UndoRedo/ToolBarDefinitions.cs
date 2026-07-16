using System.ComponentModel.Composition;
using AFOCS.Framework.Framework.ToolBars;
using AFOCS.Framework.Modules.UndoRedo.Commands;

namespace AFOCS.Framework.Modules.UndoRedo
{
    public static class ToolBarDefinitions
    {
        [Export]
        public static ToolBarItemGroupDefinition StandardUndoRedoToolBarGroup = new ToolBarItemGroupDefinition(
            ToolBars.ToolBarDefinitions.StandardToolBar, 10);

        [Export]
        public static ToolBarItemDefinition UndoToolBarItem = new CommandToolBarItemDefinition<UndoCommandDefinition>(
            StandardUndoRedoToolBarGroup, 0);

        [Export]
        public static ToolBarItemDefinition RedoToolBarItem = new CommandToolBarItemDefinition<RedoCommandDefinition>(
            StandardUndoRedoToolBarGroup, 1);
    }
}