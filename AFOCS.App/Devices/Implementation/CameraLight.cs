using AFOCS.App.Communication;
using AFOCS.App.Core;
using AFOCS.App.Extensions;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation
{
    public class CameraLightConfig
    {
        public string PortName { get; set; } = "COM100";
        public int BaudRate { get; set; } = 19200;
    }
    public class CameraLight: ICameraLight
    {
        private readonly ISerialPortClient _serialPortClient;
        private readonly IConfigService _configService;
        private readonly ILogger<CameraLight> _logger;

        public bool IsConnected => _serialPortClient.IsOpen;

        public CameraLight(ISerialPortClient serialPortClient, IConfigService configService, ILogger<CameraLight> logger)
        {
            _serialPortClient = serialPortClient;
            _configService = configService;
            _logger = logger;
        }
        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            var config = await _configService.LoadAsync<CameraLightConfig>();
            if (config == null)
            {
                config = new CameraLightConfig();
                await _configService.SaveAsync(config);
            }

            SerialPortConfig serialPortConfig = new SerialPortConfig
            {
                PortName = config.PortName,
                BaudRate = config.BaudRate,
            };
            var success = await _serialPortClient.OpenAsync(serialPortConfig, token);

            if (success)
                return Result.Success("相机光源初始化成功");
            
            return Result.Fail(ResultCode.Fail, "COM口不对");
        }

        public async Task<Result> StopAsync(CancellationToken token = default)
        {
            if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
            await _serialPortClient.CloseAsync();
            return Result.Success();
        }

        public async Task<Result> ReConnectAsync(CancellationToken token = default)
        {
            await _serialPortClient.CloseAsync();
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
                await _serialPortClient.WriteLineAsync(command);
                return Result.Success();
            }
            catch (Exception e)
            {
                _logger.LogError($"发送指令失败:{e.Message}");
                return Result.Fail(ResultCode.Fail, $"{e.Message}", e);
            }
        }
        public void Dispose()
        {
            _serialPortClient.Dispose();
        }
    }
}
