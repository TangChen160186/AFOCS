using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewTxCouplingCurveRightCommandDefinition : CommandDefinition
{
    public override string Name => "View.TxCouplingCurveRight";

    public override string Text => "右工位TX耦合曲线";

    public override string ToolTip => "打开右工位 TX 耦合曲线面板";
}
