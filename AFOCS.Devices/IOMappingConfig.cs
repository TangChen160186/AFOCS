namespace AFOCS.Devices
{
    /// <summary>
    /// IO 信号映射配置 —— 可持久化到 JSON，支持机器变化时仅改配置文件
    /// Key = 信号名称（如 "Left_EmergencyStop"），Value = 板卡位号
    /// </summary>
    public class IOMappingConfig
    {
        /// <summary>输入信号 → 位号 映射</summary>
        public Dictionary<string, int> Inputs { get; set; } = [];

        /// <summary>输出信号 → 位号 映射</summary>
        public Dictionary<string, int> Outputs { get; set; } = [];

        /// <summary>生成默认映射（从枚举值读取）</summary>
        public static IOMappingConfig CreateDefault()
        {
            var config = new IOMappingConfig();

            foreach (var signal in Enum.GetValues<Infrastructure.AllInputs>())
            {
                config.Inputs[signal.ToString()] = (int)signal;
            }

            foreach (var signal in Enum.GetValues<Infrastructure.AllOutputs>())
            {
                config.Outputs[signal.ToString()] = (int)signal;
            }

            return config;
        }
    }
}
