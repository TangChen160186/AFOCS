using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewJogStationCommandDefinition : CommandDefinition
{
    public override string Name => "View.JogStation";

    public override string Text => "轴手柄控制";

    public override string ToolTip => "打开轴手柄控制面板，可手动控制总线轴和雅克贝斯轴";
}
