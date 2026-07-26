using System.ComponentModel.Composition;
using AFOCS.Communication;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Serilog;

namespace AFOCS.Devices.Implementation
{
    public class CameraLightConfig : ICloneable
    {
        public string PortName { get; set; } = "COM7";
        public int BaudRate { get; set; } = 19200;
        public int TimeoutMs { get; set; } = 3000;

        public CameraLightConfig Clone() => new()
        {
            PortName = PortName,
            BaudRate = BaudRate,
            TimeoutMs = TimeoutMs,
        };

        object ICloneable.Clone() => Clone();
    }
    [Export(typeof(ICameraLight))]
    [method: ImportingConstructor]
    public class CameraLight(ISerialPortClient serialPortClient, IConfigService configService, ILogger logger)
        : ICameraLight
    {
        private CameraLightConfig _config = new();
        public bool IsConnected => serialPortClient.IsOpen;

        public CameraLightConfig GetConfig() => _config.Clone();

        public async Task SaveConfigAsync(CameraLightConfig config)
        {
            _config = config.Clone();
            await configService.SaveAsync(_config);
        }

        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            var config = await configService.LoadAsync<CameraLightConfig>();
            if (config == null)
            {
                config = new CameraLightConfig();
                await configService.SaveAsync(config);
            }
            _config = config;

            SerialPortConfig serialPortConfig = new SerialPortConfig
            {
                PortName = config.PortName,
                BaudRate = config.BaudRate,
            };
            var success = await serialPortClient.OpenAsync(serialPortConfig, token);

            if (success)
                return Result.Success("相机光源初始化成功");
            
            return Result.Fail(ResultCode.Fail, "COM口不对");
        }

        public async Task<Result> StopAsync(CancellationToken token = default)
        {
            if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
            await serialPortClient.CloseAsync();
            return Result.Success();
        }

        public async Task<Result> ReConnectAsync(CancellationToken token = default)
        {
            await serialPortClient.CloseAsync();
            return await InitializeAsync(token);
        }


        public async Task<Result> SetLightBrightnessAsync(CameraLightChannel channel, uint brightness)
        {
            if (!IsConnected)
                return Result.Fail(ResultCode.Fail, "未连接设备");
            if (brightness >= 255)
                brightness = 255;
            
            try
            {
                string command = $"S{channel.GetName()}{brightness:D4}#";
                await serialPortClient.WriteLineAsync(command);
                return Result.Success();
            }
            catch (Exception e)
            {
                logger.Error($"发送指令失败:{e.Message}");
                return Result.Fail(ResultCode.Fail, $"{e.Message}", e);
            }
        }
        public void Dispose()
        {
            serialPortClient.Dispose();
        }
    }
}
