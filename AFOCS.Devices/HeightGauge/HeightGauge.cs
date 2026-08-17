using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Communication;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.HeightGauge;

[Export]
[Export(typeof(IHeightGauge))]
[Description("测高仪")]
[method: ImportingConstructor]
public class HeightGauge(ITcpClient tcpClient, IConfigService configService, ILogger logger)
    : IHeightGauge
{
    private HeightGaugeConfig _config = new();
    public bool IsConnected => tcpClient.IsConnected;
    public WorkPos WorkPos => WorkPos.None;

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
            var res = await tcpClient.SendAndReceiveAsync(command,3000);

            // 返回格式形如 "MS,01,+2.03"，取 "MS,01," 之后的数值部分再转换
            var valueText = res?.Trim();
            var prefix = $"{command},";
            if (valueText != null && valueText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                valueText = valueText[prefix.Length..].Trim();

            if (valueText != null && double.TryParse(valueText, out var result))
                return Result<double>.Success(result * 1000); // 转换为um

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