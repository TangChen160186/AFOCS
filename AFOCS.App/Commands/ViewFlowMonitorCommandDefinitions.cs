using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewLeftFlowMonitorCommandDefinition : CommandDefinition
{
    public override string Name => "View.LeftFlowMonitor";

    public override string Text => "左工位流程监控";

    public override string ToolTip => "打开左工位节点执行状态监控面板";
}

[CommandDefinition]
public class ViewRightFlowMonitorCommandDefinition : CommandDefinition
{
    public override string Name => "View.RightFlowMonitor";

    public override string Text => "右工位流程监控";

    public override string ToolTip => "打开右工位节点执行状态监控面板";
}
