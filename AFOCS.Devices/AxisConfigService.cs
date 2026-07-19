using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices
{
    /// <summary>
    /// 轴配置服务 —— 管理所有总线轴的配置
    /// </summary>
    public interface IAxisConfigService
    {
        /// <summary>所有轴的当前配置（AxisId → AxisConfig）</summary>
        IReadOnlyDictionary<AxisId, AxisConfig> AxisConfigs { get; }

        /// <summary>获取指定轴的配置</summary>
        AxisConfig GetConfig(AxisId axisId);

        /// <summary>获取指定轴的运动参数</summary>
        AxisMotionParams GetMotionParams(AxisId axisId);

        /// <summary>获取指定轴的回零参数</summary>
        AxisHomeParams GetHomeParams(AxisId axisId);

        /// <summary>加载所有轴配置</summary>
        Task LoadAsync();

        /// <summary>保存所有轴配置</summary>
        Task SaveAsync();

        /// <summary>保存单个轴配置</summary>
        Task SaveAxisAsync(AxisId axisId);

        /// <summary>重置为默认值</summary>
        AxisConfig GetDefaultConfig(AxisId axisId);
    }

    [Export(typeof(IAxisConfigService))]
    [method: ImportingConstructor]
    public class AxisConfigService(IConfigService configService, ILogger logger) : IAxisConfigService
    {
        private readonly Dictionary<AxisId, AxisConfig> _configs = [];
        private const string ConfigFileName = "AxisConfigCollection";

        public IReadOnlyDictionary<AxisId, AxisConfig> AxisConfigs => _configs;

        public AxisConfig GetConfig(AxisId axisId)
        {
            if (_configs.TryGetValue(axisId, out var config))
                return config;

            var defaults = GetDefaultConfig(axisId);
            _configs[axisId] = defaults;
            return defaults;
        }

        public AxisMotionParams GetMotionParams(AxisId axisId) =>
            GetConfig(axisId).Motion;

        public AxisHomeParams GetHomeParams(AxisId axisId) =>
            GetConfig(axisId).Home;

        public async Task LoadAsync()
        {
            try
            {
                var collection = await configService.LoadAsync<AxisConfigCollection>();
                if (collection?.Axes != null)
                {
                    foreach (var (key, config) in collection.Axes)
                    {
                        _configs[(AxisId)key] = config;
                    }
                }

                // 填充缺失的轴为默认值
                foreach (AxisId axisId in Enum.GetValues<AxisId>())
                {
                    if (!_configs.ContainsKey(axisId))
                        _configs[axisId] = GetDefaultConfig(axisId);
                }

                logger.Information($"轴配置加载完成，共 {_configs.Count} 个轴");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "加载轴配置失败");
                // 全部使用默认值
                foreach (AxisId axisId in Enum.GetValues<AxisId>())
                    _configs[axisId] = GetDefaultConfig(axisId);
            }
        }

        public async Task SaveAsync()
        {
            var collection = new AxisConfigCollection
            {
                Axes = _configs.ToDictionary(kv => (int)kv.Key, kv => kv.Value)
            };
            await configService.SaveAsync(collection);
            logger.Information("轴配置已保存");
        }

        public async Task SaveAxisAsync(AxisId axisId)
        {
            await SaveAsync();
        }

        public AxisConfig GetDefaultConfig(AxisId axisId)
        {
            var config = new AxisConfig { AxisId = axisId };

            // 根据轴类型设置不同的默认脉冲当量
            switch (axisId)
            {
                // ===== 相机模组轴：1000 pulse/mm =====
                case AxisId.LeftCamUpX:
                case AxisId.RightCamUpX:
                    config.Motion.Equiv = 1000;
                    config.Motion.MaxVel = 50;
                    config.Home.HomeMode = 33;
                    config.PulsePerRev = 20000;
                    break;
                case AxisId.LeftCamUpY:
                case AxisId.RightCamUpY:
                case AxisId.LeftCamSideY:
                case AxisId.RightCamSideY:
                    config.Motion.Equiv = 1000;
                    config.Motion.MaxVel = 100;
                    config.Home.HomeMode = 33;
                    config.PulsePerRev = 10000;
                    break;
                case AxisId.LeftCamUpZ:
                case AxisId.RightCamUpZ:
                    config.Motion.Equiv = 1000;
                    config.Motion.MaxVel = 50;
                    config.Home.HomeMode = 33;
                    config.PulsePerRev = 5000;
                    break;

                // ===== 耦合θ轴：10000pul/r，1pulse=0.036度 → equiv=10000/360≈27.78 pul/度 =====
                case AxisId.LeftCouplingLThetaX:
                case AxisId.LeftCouplingLThetaY:
                case AxisId.LeftCouplingLThetaZ:
                case AxisId.LeftCouplingRThetaX:
                case AxisId.LeftCouplingRThetaY:
                case AxisId.LeftCouplingRThetaZ:
                case AxisId.RightCouplingLThetaX:
                case AxisId.RightCouplingLThetaY:
                case AxisId.RightCouplingLThetaZ:
                case AxisId.RightCouplingRThetaX:
                case AxisId.RightCouplingRThetaY:
                case AxisId.RightCouplingRThetaZ:
                    config.Motion.Equiv = 10000.0 / 360.0; // ≈27.78 pul/度
                    config.Motion.MaxVel = 30; // 度/s
                    config.Motion.MinVel = 1;
                    config.Home.HomeMode = 33;
                    config.Home.LowVel = 1;
                    config.Home.HighVel = 10;
                    config.PulsePerRev = 10000;
                    break;
            }

            return config;
        }
    }
}
