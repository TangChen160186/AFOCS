using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewLeftStationMonitorCommandDefinition : CommandDefinition
{
    public override string Name => "View.LeftStationMonitor";

    public override string Text => "左工位 RSP/MPD 监控";

    public override string ToolTip => "打开左工位 RSP / MPD_IN / MPD_OUT 监控面板";
}

[CommandDefinition]
public class ViewRightStationMonitorCommandDefinition : CommandDefinition
{
    public override string Name => "View.RightStationMonitor";

    public override string Text => "右工位 RSP/MPD 监控";

    public override string ToolTip => "打开右工位 RSP / MPD_IN / MPD_OUT 监控面板";
}
