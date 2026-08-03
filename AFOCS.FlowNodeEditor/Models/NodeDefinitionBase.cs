using System.ComponentModel;
using Caliburn.Micro;

namespace AFOCS.FlowNodeEditor.Models;

public class NodeDefinitionBase : PropertyChangedBase,INodeDefinition
{
    [DisplayName("描述")]
    public string Description
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    [DisplayName("启用")]
    public bool Enabled
    {
        get;
        set => Set(ref field, value);
    } = true;

    [Browsable(false)]
    public override bool IsNotifying { get; set; }
}