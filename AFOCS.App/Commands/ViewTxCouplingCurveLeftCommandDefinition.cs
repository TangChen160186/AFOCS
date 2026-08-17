using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewTxCouplingCurveLeftCommandDefinition : CommandDefinition
{
    public override string Name => "View.TxCouplingCurveLeft";

    public override string Text => "左工位TX耦合曲线";

    public override string ToolTip => "打开左工位 TX 耦合曲线面板";
}
