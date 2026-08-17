using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewRxCouplingCurveLeftCommandDefinition : CommandDefinition
{
    public override string Name => "View.RxCouplingCurveLeft";

    public override string Text => "左工位耦合曲线";

    public override string ToolTip => "打开左工位 RX 耦合曲线面板";
}
