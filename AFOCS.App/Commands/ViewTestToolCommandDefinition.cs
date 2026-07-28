using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewTestToolCommandDefinition : CommandDefinition
{
    public override string Name => "View.TestTool";

    public override string Text => "功能测试";

    public override string ToolTip => "打开功能测试面板，用于开发和调试";
}
