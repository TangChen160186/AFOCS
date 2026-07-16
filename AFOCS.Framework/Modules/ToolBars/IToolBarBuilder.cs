using AFOCS.Framework.Framework.ToolBars;

namespace AFOCS.Framework.Modules.ToolBars
{
    public interface IToolBarBuilder
    {
        void BuildToolBars(IToolBars result);
        void BuildToolBar(ToolBarDefinition toolBarDefinition, IToolBar result);
    }
}