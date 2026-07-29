using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewHomeTestCommandDefinition : CommandDefinition
{
    public override string Name => "View.HomeTest";

    public override string Text => "回零测试";

    public override string ToolTip => "打开回零测试面板，列出所有轴并支持单独/全部回零";
}
