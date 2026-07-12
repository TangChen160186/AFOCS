using AFOCS.App.Communication;
using AFOCS.App.Core;
using AFOCS.App.Enums;
using AFOCS.App.Extensions;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation
{
    public class CameraLight: ICameraLight
    {
        private readonly ISerialPortClient _serialPortClient;
        private readonly IConfigService _configService;
        private readonly ILogger<GlueDispenser> _logger;
        private readonly IIoController _ioController;

        public bool IsConnected => _serialPortClient.IsOpen;
        public EDeviceType Type => EDeviceType.CameraLight;
        public WorkPos WorkPos => WorkPos.Common;

        private CameraLightConfig? _config;
        public CameraLight(ISerialPortClient serialPortClient, IConfigService configService, ILogger<GlueDispenser> logger,IIoController ioController)
        {
            _serialPortClient = serialPortClient;
            _configService = configService;
            _logger = logger;
            _ioController = ioController;
        }
        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            _config = await _configService.LoadAsync<CameraLightConfig>();
            if (_config == null)
            {
                _config = CameraLightConfig.Default;
                await _configService.SaveAsync(_config);
            }

            SerialPortConfig serialPortConfig = new SerialPortConfig
            {
                PortName = _config.PortName,
                BaudRate = _config.BaudRate,
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


        public Task<Result> OpenAsync(CameraAndLightPos pos)
        {
            // 利用IO打开
            throw new NotImplementedException();
        }

        public async Task<Result> SetLightBrightnessAsync(CameraAndLightPos pos, uint brightness)
        {
            if (!IsConnected)
                return Result.Fail(ResultCode.Fail, "未连接设备");
            if (brightness >= 255)
                brightness = 255;
            
            try
            {
                var channel = _config!.ChannelMap[pos].GetName();
                string command = $"S{channel}{brightness:D4}#";
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
