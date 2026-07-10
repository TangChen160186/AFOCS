using AFOCS.App.Communication;
using AFOCS.App.Core;
using AFOCS.App.Shared;
using AFOCS.App.Enums;
using Caliburn.Micro;
using Microsoft.Extensions.Logging;
using Result = AFOCS.App.Core.Result;

namespace AFOCS.App.Devices
{
    public class GlueDispenser : IGlueDispenser
    {
        public bool IsConnected { get; private set; }
        public EDeviceType Type => EDeviceType.GlueDispenser;
        public WorkPos WorkPos { get; }

        private readonly ISerialPortClient _serialPortClient;
        private readonly IConfigService _configService;
        private readonly ILogger<GlueDispenser> _logger;


        public GlueDispenser(WorkPos workPos)
        {
            _serialPortClient = IoC.Get<ISerialPortClient>();
            _configService = IoC.Get<IConfigService>();
            _logger = IoC.Get<ILogger<GlueDispenser>>();
            WorkPos = workPos;
        }
        public void Dispose()
        {
            _serialPortClient.Dispose();
        }


        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            var config =
                await _configService.LoadAsync<GlueDispenserConfig>();
            if (config == null)
            {
                config = GlueDispenserConfig.Default;
                await _configService.SaveAsync(config);
            }

            SerialPortConfig serialPortConfig = new SerialPortConfig
            {
                PortName = WorkPos == WorkPos.Left ? config.LeftConfig.PortName : config.RightConfig.PortName,
                BaudRate = WorkPos == WorkPos.Left ? config.LeftConfig.BaudRate : config.RightConfig.BaudRate,
            };
            var success = await _serialPortClient.OpenAsync(serialPortConfig, token);

            if (success)
            {
                IsConnected = true;
                return Result.Success("点胶机初始化成功");
            }
            return Result.Fail(ResultCode.Fail, "COM口不对");
        }

        public async Task<Result> StopAsync(CancellationToken token = default)
        {
            if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
            await _serialPortClient.CloseAsync();
            IsConnected = false;
            return Result.Success();
        }

        public async Task<Result> ReConnectAsync(CancellationToken token = default)
        {
            await _serialPortClient.CloseAsync();
            IsConnected = false;
            return await InitializeAsync(token);
        }

        public async Task<Result> ShotAsync()
        {
            if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = "M,0000";
                var length = await _serialPortClient.WriteLineAsync(command);
                _logger.LogTrace($"发送指令:{command},长度:{length}");
                return Result.Success();
            }
            catch (Exception e)
            {
                _logger.LogError($"发送指令失败:{e.Message}");
                return Result.Fail(ResultCode.Fail, $"{e.Message}", e);
            }
       
        }
    }
}
