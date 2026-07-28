using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewTeachingPointsCommandDefinition : CommandDefinition
{
    public override string Name => "View.TeachingPoints";

    public override string Text => "示教点";

    public override string ToolTip => "打开示教点编辑界面";
}
