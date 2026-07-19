using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices
{
    /// <summary>
    /// IO 映射服务 —— 管理 AllInputs/AllOutputs 到底层位号的映射
    /// 映射关系持久化到 Configs/IOMappingConfig.json，机器变动时修改配置文件即可
    /// </summary>
    public interface IIOMappingService
    {
        /// <summary>获取输入信号的板卡位号</summary>
        int GetInputBitNo(AllInputs signal);

        /// <summary>获取输出信号的板卡位号</summary>
        int GetOutputBitNo(AllOutputs signal);

        /// <summary>设置输入信号的位号</summary>
        void SetInputBitNo(AllInputs signal, int bitNo);

        /// <summary>设置输出信号的位号</summary>
        void SetOutputBitNo(AllOutputs signal, int bitNo);

        /// <summary>获取完整配置（用于界面展示）</summary>
        IOMappingConfig GetConfig();

        /// <summary>加载配置</summary>
        Task LoadAsync();

        /// <summary>保存配置</summary>
        Task SaveAsync();

        /// <summary>写入输出信号（自动解析位号）</summary>
        Task WriteOutputAsync(AllOutputs signal, bool on);
    }

    [Export(typeof(IIOMappingService))]
    [method: ImportingConstructor]
    public class IOMappingService(IConfigService configService, IMotionControlCard motionCard, ILogger logger) : IIOMappingService
    {
        private IOMappingConfig _config = IOMappingConfig.CreateDefault();

        private readonly Dictionary<AllInputs, int> _inputLookup = [];
        private readonly Dictionary<AllOutputs, int> _outputLookup = [];

        public int GetInputBitNo(AllInputs signal) =>
            _inputLookup.TryGetValue(signal, out var bitNo) ? bitNo : (int)signal;

        public int GetOutputBitNo(AllOutputs signal) =>
            _outputLookup.TryGetValue(signal, out var bitNo) ? bitNo : (int)signal;

        public void SetInputBitNo(AllInputs signal, int bitNo)
        {
            _config.Inputs[signal.ToString()] = bitNo;
            _inputLookup[signal] = bitNo;
        }

        public void SetOutputBitNo(AllOutputs signal, int bitNo)
        {
            _config.Outputs[signal.ToString()] = bitNo;
            _outputLookup[signal] = bitNo;
        }

        public IOMappingConfig GetConfig() => _config;

        public async Task LoadAsync()
        {
            try
            {
                var loaded = await configService.LoadAsync<IOMappingConfig>();
                if (loaded?.Inputs is { Count: > 0 } || loaded?.Outputs is { Count: > 0 })
                {
                    _config = loaded;
                    logger.Information("IO 映射配置已加载，输入 {InputCount} 项，输出 {OutputCount} 项",
                        _config.Inputs.Count, _config.Outputs.Count);
                }
                else
                {
                    _config = IOMappingConfig.CreateDefault();
                    await configService.SaveAsync(_config);
                    logger.Information("IO 映射配置已初始化为默认值");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "加载 IO 映射配置失败，使用默认值");
                _config = IOMappingConfig.CreateDefault();
            }

            RebuildLookup();
        }

        public async Task SaveAsync()
        {
            foreach (var (signal, bitNo) in _inputLookup)
                _config.Inputs[signal.ToString()] = bitNo;
            foreach (var (signal, bitNo) in _outputLookup)
                _config.Outputs[signal.ToString()] = bitNo;

            await configService.SaveAsync(_config);
            logger.Information("IO 映射配置已保存");
        }

        public async Task WriteOutputAsync(AllOutputs signal, bool on)
        {
            var bitNo = GetOutputBitNo(signal);
            var result = await motionCard.WriteOutbitAsync((ushort)bitNo, on);
            if (result.IsSuccess)
                logger.Information("IO 输出: {Signal}(bit{No}) = {Value}", signal, bitNo, on);
            else
                logger.Warning("IO 输出失败: {Signal} bit{No}, {Error}", signal, bitNo, result.Message);
        }

        private void RebuildLookup()
        {
            _inputLookup.Clear();
            _outputLookup.Clear();

            foreach (var kv in _config.Inputs)
            {
                if (Enum.TryParse<AllInputs>(kv.Key, out var signal))
                    _inputLookup[signal] = kv.Value;
            }
            foreach (var kv in _config.Outputs)
            {
                if (Enum.TryParse<AllOutputs>(kv.Key, out var signal))
                    _outputLookup[signal] = kv.Value;
            }
        }
    }
}
