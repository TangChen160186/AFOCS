using AFOCS.Framework.Framework.Commands;

namespace AFOCS.VisionEditor.Commands;

[CommandDefinition]
public class ViewVisionEditorCommandDefinition : CommandDefinition
{
    public override string Name => "View.VisionEditor";

    public override string Text => "视觉编辑器";

    public override string ToolTip => "打开视觉模板编辑器";
}
