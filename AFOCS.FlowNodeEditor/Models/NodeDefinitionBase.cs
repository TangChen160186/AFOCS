using System.ComponentModel;
using System.Text.Json.Serialization;
using Caliburn.Micro;

namespace AFOCS.FlowNodeEditor.Models;

public class NodeDefinitionBase : PropertyChangedBase,INodeDefinition
{
    [DisplayName("描述")]
    [Category("基础")]
    public string Description
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    [DisplayName("启用")]
    [Category("基础")]
    public bool Enabled
    {
        get;
        set => Set(ref field, value);
    } = true;

    [JsonIgnore]
    [Browsable(false)]
    public override bool IsNotifying { get; set; }
}