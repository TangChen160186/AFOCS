using AFOCS.Infrastructure;

namespace AFOCS.Devices.MotionControlCard;

[ConfigPath("设备/雷赛板卡")]
public class LeadShineMotionCardConfig : ICloneable
{
    public string EniPath { get; set; } = "";
    public string IniPath { get; set; } = "";
    public int TimeoutMs { get; set; } = 30000;

    public LeadShineMotionCardConfig Clone() => new()
    {
        EniPath = EniPath,
        IniPath = IniPath,
        TimeoutMs = TimeoutMs,
    };

    object ICloneable.Clone() => Clone();
}