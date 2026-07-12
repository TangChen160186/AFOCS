using AFOCS.App.Communication;
using AFOCS.App.Core;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;
using Result = AFOCS.App.Core.Result;

namespace AFOCS.App.Devices.Implementation
{
    public class GlueDispenserConfig
    {
        public string PortName { get; set; } = "COM100";
        public int BaudRate { get; set; } = 9600;
    }
    public class GlueDispenser<T> : IGlueDispenser where T: GlueDispenserConfig, new()
    {
        public bool IsConnected => _serialPortClient.IsOpen;

        private readonly ISerialPortClient _serialPortClient;
        private readonly IConfigService _configService;
        private readonly ILogger<GlueDispenser<T>> _logger;

        public GlueDispenser(ISerialPortClient serialPortClient, IConfigService configService, ILogger<GlueDispenser<T>> logger)
        {
            _serialPortClient = serialPortClient;
            _configService = configService;
            _logger = logger;
        }

        public void Dispose()
        {
            _serialPortClient.Dispose();
        }

        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            var config = await _configService.LoadAsync<T>();
            if (config == null)
            {
                config = new T();
                await _configService.SaveAsync(config);
            }

            SerialPortConfig serialPortConfig = new SerialPortConfig
            {
                PortName = config.PortName,
                BaudRate = config.BaudRate,
            };
            var success = await _serialPortClient.OpenAsync(serialPortConfig, token);

            if (success)
            {
                return Result.Success("点胶机初始化成功");
            }
            return Result.Fail(ResultCode.Fail, "COM口不对");
        }

        public async Task<Result> StopAsync(CancellationToken token = default)
        {
            if (!IsConnected) 
                return Result.Fail(ResultCode.Fail, "未连接设备");
            await _serialPortClient.CloseAsync();
            return Result.Success();
        }

        public async Task<Result> ReConnectAsync(CancellationToken token = default)
        {
            await _serialPortClient.CloseAsync();
            return await InitializeAsync(token);
        }

        public async Task<Result> ShotAsync()
        {
            if (!IsConnected) 
                return Result.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = "M,0000";
                await _serialPortClient.WriteLineAsync(command);
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