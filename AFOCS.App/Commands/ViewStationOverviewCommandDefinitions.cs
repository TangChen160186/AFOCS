using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewLeftStationOverviewCommandDefinition : CommandDefinition
{
    public override string Name => "View.LeftStationOverview";

    public override string Text => "左工位总览";

    public override string ToolTip => "打开左工位总览窗口（流程监视、相机、RSP/MPD、耦合曲线）";
}

[CommandDefinition]
public class ViewRightStationOverviewCommandDefinition : CommandDefinition
{
    public override string Name => "View.RightStationOverview";

    public override string Text => "右工位总览";

    public override string ToolTip => "打开右工位总览窗口（流程监视、相机、RSP/MPD、耦合曲线）";
}