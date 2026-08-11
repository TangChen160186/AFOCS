using AFOCS.Infrastructure;

namespace AFOCS.Devices.IO;

/// <summary>
/// IO 信号映射配置 —— 可持久化到 JSON，支持机器变化时仅改配置文件
/// Key = 信号名称（如 "Left_EmergencyStop"），Value = 板卡位号
/// </summary>
///
[ConfigPath("设备/IO")]
public class IoMappingConfig
{
    /// <summary>输入信号 → 位号 映射</summary>
    public Dictionary<string, int> Inputs { get; set; } = [];

    /// <summary>输出信号 → 位号 映射</summary>
    public Dictionary<string, int> Outputs { get; set; } = [];

    /// <summary>输入信号 → 是否高电平有效（true=高有效，false=低有效），默认 true</summary>
    public Dictionary<string, bool> InputActives { get; set; } = [];

    /// <summary>输出信号 → 是否高电平有效（true=高有效，false=低有效），默认 true</summary>
    public Dictionary<string, bool> OutputActives { get; set; } = [];

    /// <summary>生成默认映射（从枚举值读取）</summary>
    public static IoMappingConfig CreateDefault()
    {
        var config = new IoMappingConfig();

        foreach (var signal in Enum.GetValues<AllInputs>())
        {
            config.Inputs[signal.ToString()] = (int)signal;
            config.InputActives[signal.ToString()] = true;
        }

        foreach (var signal in Enum.GetValues<AllOutputs>())
        {
            config.Outputs[signal.ToString()] = (int)signal;
            config.OutputActives[signal.ToString()] = true;
        }

        return config;
    }
}