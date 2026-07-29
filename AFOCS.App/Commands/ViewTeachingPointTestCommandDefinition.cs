using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewTeachingPointTestCommandDefinition : CommandDefinition
{
    public override string Name => "View.TeachingPointTest";

    public override string Text => "示教点测试";

    public override string ToolTip => "打开示教点测试面板，用于测试示教点运动";
}
