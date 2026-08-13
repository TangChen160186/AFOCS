using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewFaPdCalibrationCommandDefinition : CommandDefinition
{
    public override string Name => "View.FaPdCalibration";

    public override string Text => "FA下表面PD测高标定";

    public override string ToolTip => "打开 FA 下表面到 PD 测高的标定面板";
}
