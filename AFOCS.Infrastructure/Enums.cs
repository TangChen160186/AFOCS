using System.ComponentModel;

namespace AFOCS.Infrastructure;

public enum WorkPos
{
    [Description("通用")]
    None,
    [Description("左工位")]
    Left,
    [Description("右工位")]
    Right,
}
