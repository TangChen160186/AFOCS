using System.ComponentModel.Composition;
using AFOCS.Communication;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation
{
    public class HeightGaugeConfig : ICloneable
    {
        public string Ip { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 1000;
        public int TimeoutMs { get; set; } = 3000;

        public HeightGaugeConfig Clone() => new()
        {
            Ip = Ip,
            Port = Port,
            TimeoutMs = TimeoutMs,
        };

        object ICloneable.Clone() => Clone();
    }
    [Export]
    [Export(typeof(IHeightGauge))]
    [method: ImportingConstructor]
    public class HeightGauge(ITcpClient tcpClient, IConfigService configService, ILogger logger)
        : IHeightGauge
    {
        private HeightGaugeConfig _config = new();
        public bool IsConnected => tcpClient.IsConnected;

        public HeightGaugeConfig GetConfig() => _config.Clone();

        public async Task SaveConfigAsync(HeightGaugeConfig config)
        {
            _config = config.Clone();
            await configService.SaveAsync(_config);
        }

        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            var config = await configService.LoadAsync<HeightGaugeConfig>();
            if (config == null)
            {
                config = new HeightGaugeConfig();
                await configService.SaveAsync(config);
            }
            _config = config;

            TcpClientConfig tcpClientConfig = new TcpClientConfig
            {
                IpAddress = config.Ip,
                Port = config.Port,
            };
            var success = await tcpClient.ConnectAsync(tcpClientConfig);
            if (success)
                return Result.Success("测高仪初始化成功");

            return Result.Fail(ResultCode.Fail, "TCP连接失败");
        }

        public async Task<Result> StopAsync(CancellationToken token = default)
        {
            if (!IsConnected)
                return Result.Fail(ResultCode.Fail, "未连接设备");
            await tcpClient.DisconnectAsync();
            return Result.Success();
        }

        public async Task<Result> ReConnectAsync(CancellationToken token = default)
        {
            await tcpClient.DisconnectAsync();
            return await InitializeAsync(token);
        }


        public async Task<Result<double>> GetHeightAsync(int channel)
        {
            if (!IsConnected)
                return Result<double>.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $"MS,0{channel}";
                var res = await tcpClient.SendAndReceiveAsync(command);
                if (double.TryParse(res, out var power))
                    return Result<double>.Success(power);
                return Result<double>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
            }
            catch (Exception e)
            {
                logger.Error(e.Message);
                return Result<double>.Fail(ResultCode.Fail, e.Message, e);
            }
        }

        public void Dispose()
        {
            tcpClient.Dispose();
        }
    }
}
