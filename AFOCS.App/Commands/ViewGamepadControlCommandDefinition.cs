using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewGamepadControlCommandDefinition : CommandDefinition
{
    public override string Name => "View.GamepadControl";

    public override string Text => "手柄控制";

    public override string ToolTip => "打开手柄控制面板，用于手动控制轴运动";
}
