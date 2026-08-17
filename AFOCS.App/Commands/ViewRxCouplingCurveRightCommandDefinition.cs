using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewRxCouplingCurveRightCommandDefinition : CommandDefinition
{
    public override string Name => "View.RxCouplingCurveRight";

    public override string Text => "右工位耦合曲线";

    public override string ToolTip => "打开右工位 RX 耦合曲线面板";
}
