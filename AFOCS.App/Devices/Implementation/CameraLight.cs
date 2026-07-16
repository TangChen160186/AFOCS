using System.ComponentModel.Composition;
using AFOCS.App.Communication;
using AFOCS.App.Core;
using AFOCS.App.Extensions;
using AFOCS.App.Shared;
using Serilog;

namespace AFOCS.App.Devices.Implementation
{
    public class CameraLightConfig
    {
        public string PortName { get; set; } = "COM100";
        public int BaudRate { get; set; } = 19200;
    }
    [Export]
    [method: ImportingConstructor]
    public class CameraLight(ISerialPortClient serialPortClient, IConfigService configService, ILogger logger)
        : ICameraLight
    {
        public bool IsConnected => serialPortClient.IsOpen;

        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            var config = await configService.LoadAsync<CameraLightConfig>();
            if (config == null)
            {
                config = new CameraLightConfig();
                await configService.SaveAsync(config);
            }

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
