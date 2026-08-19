using AFOCS.Framework.Framework.Commands;

namespace AFOCS.App.Commands;

[CommandDefinition]
public class CameraViewerCommandDefinition : CommandDefinition
{
    public override string Name => "View.CameraViewer";

    public override string Text => "相机查看";

    public override string ToolTip => "打开相机查看工具，支持下拉切换相机与右键保存图像";
}
