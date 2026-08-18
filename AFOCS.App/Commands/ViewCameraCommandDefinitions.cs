using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class ViewLeftUpCameraCommandDefinition : CommandDefinition
{
    public override string Name => "View.LeftUpCamera";

    public override string Text => "左上相机实时图像";

    public override string ToolTip => "打开左上相机实时监控面板";
}

[CommandDefinition]
public class ViewLeftDownCameraCommandDefinition : CommandDefinition
{
    public override string Name => "View.LeftDownCamera";

    public override string Text => "左下相机实时图像";

    public override string ToolTip => "打开左下相机实时监控面板";
}

[CommandDefinition]
public class ViewRightUpCameraCommandDefinition : CommandDefinition
{
    public override string Name => "View.RightUpCamera";

    public override string Text => "右上相机实时图像";

    public override string ToolTip => "打开右上相机实时监控面板";
}

[CommandDefinition]
public class ViewRightDownCameraCommandDefinition : CommandDefinition
{
    public override string Name => "View.RightDownCamera";

    public override string Text => "右下相机实时图像";

    public override string ToolTip => "打开右下相机实时监控面板";
}
