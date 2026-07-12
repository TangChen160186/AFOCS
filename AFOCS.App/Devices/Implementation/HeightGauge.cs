using AFOCS.App.Communication;
using AFOCS.App.Core;
using AFOCS.App.Enums;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation
{
    public class HeightGauge : IHeightGauge
    {
        private readonly ITcpClient _tcpClient;
        private readonly IConfigService _configService;
        private readonly ILogger<OpticalSwitch> _logger;

        public bool IsConnected => _tcpClient.IsConnected;
        public EDeviceType Type => EDeviceType.HeightGauge;

        public HeightGauge(ITcpClient tcpClient, IConfigService configService, ILogger<OpticalSwitch> logger)
        {
            _tcpClient = tcpClient;
            _configService = configService;
            _logger = logger;
        }

        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            var config = await _configService.LoadAsync<HeightGaugeConfig>();
            if (config == null)
            {
                config = HeightGaugeConfig.Default;
                await _configService.SaveAsync(config);
            }

            TcpClientConfig tcpClientConfig = new TcpClientConfig
            {
                IpAddress = config.Ip,
                Port = config.Port,
            };
            var success = await _tcpClient.ConnectAsync(tcpClientConfig);
            if (success)
                return Result.Success("测高仪初始化成功");

            return Result.Fail(ResultCode.Fail, "TCP连接失败");
        }

        public async Task<Result> StopAsync(CancellationToken token = default)
        {
            if (!IsConnected)
                return Result.Fail(ResultCode.Fail, "未连接设备");
            await _tcpClient.DisconnectAsync();
            return Result.Success();
        }

        public async Task<Result> ReConnectAsync(CancellationToken token = default)
        {
            await _tcpClient.DisconnectAsync();
            return await InitializeAsync(token);
        }


        public async Task<Result<double>> GetHeightAsync(int channel)
        {
            if (!IsConnected)
                return Result<double>.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $"MS,0{channel}";
                var res = await _tcpClient.SendAndReceiveAsync(command);
                if (double.TryParse(res, out var power))
                    return Result<double>.Success(power);
                return Result<double>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return Result<double>.Fail(ResultCode.Fail, e.Message, e);
            }
        }

        public void Dispose()
        {
            _tcpClient.Dispose();
        }
    }
}
