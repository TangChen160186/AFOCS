using System.ComponentModel.Composition;
using System.Text;
using AFOCS.Communication;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.OpticalSwitch;

public class OpticalSwitchConfig : ICloneable
{
    public string Ip { get; set; } = "192.168.1.188";
    public int Port { get; set; } = 1000;
    public int TimeoutMs { get; set; } = 3000;

    public OpticalSwitchConfig Clone() => new()
    {
        Ip = Ip,
        Port = Port,
        TimeoutMs = TimeoutMs,
    };

    object ICloneable.Clone() => Clone();
}
[Export(typeof(IOpticalSwitch))]
[method: ImportingConstructor]
public class OpticalSwitch(ITcpClient tcpClient, IConfigService configService, ILogger logger)
    : IOpticalSwitch
{
    private OpticalSwitchConfig _config = new();
    public bool IsConnected => tcpClient.IsConnected;
    public WorkPos WorkPos { get; }

    public OpticalSwitchConfig GetConfig() => _config.Clone();

    public async Task SaveConfigAsync(OpticalSwitchConfig config)
    {
        _config = config.Clone();
        await configService.SaveAsync(_config);
    }

    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        var config = await configService.LoadAsync<OpticalSwitchConfig>();
        if (config == null)
        {
            config = new OpticalSwitchConfig();
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
            return Result.Success("光开关初始化成功");

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

    public void Dispose()
    {
        tcpClient.Dispose();
    }


    public async Task<Result<bool>> SwitchChannelAsync(int group, int channel)
    {
        if (!IsConnected)
            return Result<bool>.Fail(ResultCode.Fail, "设备未连接");
        try
        {
            string command = $"SW {group:D2} {channel:D2}";
            var result = await tcpClient.SendAndReceiveAsync(command);

            if(string.IsNullOrWhiteSpace(result))
                return Result<bool>.Fail(ResultCode.Fail, "返回的数据为空");
            var trimResult = result.Trim();
            var split = trimResult.Split(" ");


            if (split.Length != 3 || !int.TryParse(split[1], out var g) || !int.TryParse(split[2], out var c))
                return Result<bool>.Fail(ResultCode.Fail, $"返回数据未知格式:{result}");

            if(g == group && c == channel) 
                return Result<bool>.Success(true, "切换通道成功");
            return Result<bool>.Success(false, "切换通道失败,返回的数据和传入不一致");
        }
        catch (Exception e)
        {
            logger.Error(e.Message);
            return Result<bool>.Fail(ResultCode.Fail, e.Message, e);
        }
            
    }

    public async Task<Result<bool>> SwitchChannelAsync(int[] groups, int[] channels)
    {
        ArgumentException.ThrowIfNullOrEmpty(nameof(groups));
        ArgumentException.ThrowIfNullOrEmpty(nameof(channels));

        if (!IsConnected) 
            return Result<bool>.Fail(ResultCode.Fail, "设备未连接");
        if (groups.Length > 16 || channels.Length > 16 || groups.Length != channels.Length)
            return Result<bool>.Fail(ResultCode.Fail, "参数个数不对");
        try
        {
            StringBuilder command = new StringBuilder("SW");
            int length = groups.Length;
            for (int i = 0; i < length; i++)
            {
                int group = groups[i];
                int channel = channels[i];
                command.Append($" {group:D2} {channel:D2}");
            }
            var result = await tcpClient.SendAndReceiveAsync(command.ToString());
            if (string.IsNullOrWhiteSpace(result))
                return Result<bool>.Fail(ResultCode.Fail, "返回的数据为空");

            var trimResult = result.Trim();
            var split = trimResult.Split(" ");
            if (split.Length != length * 2 + 1)
                return Result<bool>.Fail(ResultCode.Fail, $"返回数据未知格式:{result}");
            for (int i = 0; i < length; i++)
            {
                int group = groups[i];
                int channel = channels[i];
                if (!int.TryParse(split[i*2+1], out var g) || !int.TryParse(split[i*2+2], out var c))
                    return Result<bool>.Fail(ResultCode.Fail, $"返回数据未知格式:{result}");
                if(group!=g || channel!= c)
                    return Result<bool>.Success(false, $"通道切换失败,返回的数据和传入不一致");
            }
            return Result<bool>.Success(true, "切换通道成功");
        }
        catch (Exception e)
        {
            logger.Error(e.Message);
            return Result<bool>.Fail(ResultCode.Fail, e.Message, e);
        }
            
    }

    public async Task<Result<Dictionary<int, int>>> GetAllChannelStatusAsync()
    {
        if (!IsConnected) 
            return Result<Dictionary<int, int>>.Fail(ResultCode.Fail, "设备未连接");

        try
        {
            string command = new string("SW ?");
            logger.Verbose($"发送指令:{command}");
            var result = await tcpClient.SendAndReceiveAsync(command);

            if (string.IsNullOrWhiteSpace(result))
                return Result<Dictionary<int, int>>.Fail(ResultCode.Fail, "返回的数据为空");
            var trimResult = result.Trim();
            var split = trimResult.Split(" ");

            if (split.Length != 32)
                return Result<Dictionary<int, int>>.Fail(ResultCode.Fail, $"返回数据未知格式:{result}");

            Dictionary<int,int> channelStatus = new Dictionary<int,int>();

            for (int i = 0; i < 16; i++)
            {
                if (!int.TryParse(split[i + 1], out var g) || !int.TryParse(split[i + 2], out var c))
                    return Result<Dictionary<int, int>>.Fail(ResultCode.Fail, $"返回数据未知格式:{result}");
                channelStatus[g] = c;
            }

            return Result<Dictionary<int,int>>.Success(channelStatus);
        }
        catch (Exception e)
        {
            logger.Error(e.Message);
            return Result<Dictionary<int, int>>.Fail(ResultCode.Fail, e.Message, e);
        }
           
    }

    public async Task<Result<string>> GetSnAsync()
    {
        if (!IsConnected) 
            return Result<string>.Fail(ResultCode.Fail, "设备未连接");
        try
        {
            string command = new string("SN ?");
            logger.Verbose($"发送指令:{command}");
            var result = await tcpClient.SendAndReceiveAsync(command);
            return Result<string>.Success(result);
        }
        catch (Exception e)
        {
            logger.Error(e.Message);
            return Result<string>.Fail(ResultCode.Fail, e.Message, e);
        }
    }

    public async Task<Result<string>> GetPnAsync()
    {
        if (!IsConnected)
            return Result<string>.Fail(ResultCode.Fail, "设备未连接");
        try
        {
            string command = new string("PN ?");
            logger.Verbose($"发送指令:{command}");
            var result = await tcpClient.SendAndReceiveAsync(command);
            return Result<string>.Success(result);
        }
        catch (Exception e)
        {
            logger.Error(e.Message);
            return Result<string>.Fail(ResultCode.Fail, e.Message, e);
        }
    }
}