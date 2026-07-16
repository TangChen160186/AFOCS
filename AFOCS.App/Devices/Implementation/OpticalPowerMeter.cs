using System.ComponentModel.Composition;
using AFOCS.App.Communication;
using AFOCS.App.Core;
using AFOCS.App.Shared;
using Serilog;

namespace AFOCS.App.Devices.Implementation
{
    public class OpticalPowerMeterConfig
    {
        public string Ip { get; set; } = "192.168.0.200";
        public int Port { get; set; } = 3498;
    }

    public class OpticalPowerMeter<T>(ITcpClient tcpClient, IConfigService configService, ILogger logger)
        : IOpticalPowerMeter
        where T : OpticalPowerMeterConfig, new()
    {
        public bool IsConnected => tcpClient.IsConnected;

        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            var config = await configService.LoadAsync<T>();
            if (config == null)
            {
                config = new T();
                await configService.SaveAsync(config);
            }

            TcpClientConfig tcpClientConfig = new TcpClientConfig
            {
                IpAddress = config.Ip,
                Port = config.Port,
            };
            var success = await tcpClient.ConnectAsync(tcpClientConfig);
            if (success)
                return Result.Success("光功率计机箱初始化成功");
            

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

        #region OS光源
        public async Task<Result<bool>> GetOsReadyAsync(int slot)
        {
            if (!IsConnected) 
                return Result<bool>.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:READY?";
                var res = await tcpClient.SendAndReceiveAsync(command);

                if(string.IsNullOrWhiteSpace(res) || !res.Equals("1") || !res.Equals("0"))
                    return Result<bool>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
                return Result<bool>.Success(res.Equals("1"));
            }
            catch (Exception e)
            {
                logger.Error(e.Message);
                return Result<bool>.Fail(ResultCode.Fail, e.Message, e);
            }
        }

        public async Task<Result<bool>> GetOsStatusAsync(int slot, int channel)
        {
            if (!IsConnected) 
                return Result<bool>.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:CHANnel{channel}:STATus?";
                var res = await tcpClient.SendAndReceiveAsync(command);
                if (string.IsNullOrWhiteSpace(res) || !res.Equals("1") || !res.Equals("0"))
                    return Result<bool>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
                return Result<bool>.Success(res.Equals("1"));
            }
            catch (Exception e)
            {
                logger.Error(e.Message);
                return Result<bool>.Fail(ResultCode.Fail, e.Message, e);
            }
        }

        public async Task<Result> SetOsPowerAsync(int slot, int channel, double power)
        {
            if (!IsConnected) 
                return Result.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:CHANnel{channel}:POWer {power:F3}";
                logger.Verbose($"发送指令:{command}");
                await tcpClient.WriteLineAsync(command);
                return Result.Success();
            }
            catch (Exception e)
            {
                logger.Error(e.Message);
                return Result.Fail(ResultCode.Fail, e.Message, e);
            }
        }

        public async Task<Result<double>> GetOsPowerAsync(int slot, int channel)
        {
            if (!IsConnected) 
                return Result<double>.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:CHANnel{channel}:POWer?";
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

        public async Task<Result<double[]>> GetOsPowerAsync(int slot)
        {
            if (!IsConnected) 
                return Result<double[]>.Fail(ResultCode.Fail, "未连接设备");
            List<double> powers = new List<double>();
            try
            {
                string command = $":SLOT{slot}:POWer?";
                var res = await tcpClient.SendAndReceiveAsync(command);
                var splits = res.Split(",");
                if (splits.Length < 1)
                    return Result<double[]>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
                foreach (var split in splits)
                {
                    if (double.TryParse(split, out var power))
                        powers.Add(power);
                    else
                        return Result<double[]>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
                }
                return Result<double[]>.Success(powers.ToArray());
            }
            catch (Exception e)
            {
                logger.Error(e.Message);
                return Result<double[]>.Fail(ResultCode.Fail, e.Message, e);
            }
        }


        #endregion

        #region 功率计
        public Task<Result<bool>> GetOpmReadyAsync(int slot)
            => GetOsReadyAsync(slot);

        public Task<Result<double>> GetOpmPowerAsync(int slot, int channel)
            => GetOsPowerAsync(slot, channel);
        public Task<Result<double[]>> GetOpmPowerAsync(int slot)
            => GetOsPowerAsync(slot);
        

        public async Task<Result> SetOpmOffsetAsync(int slot, int channel, double offset)
        {
            if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:CHANnel{channel}:OFFSET {offset:F3}";
                await tcpClient.WriteLineAsync(command);
                return Result.Success();
            }
            catch (Exception e)
            {
                logger.Error(e.Message);
                return Result.Fail(ResultCode.Fail, e.Message, e);
            }
        }

        public async Task<Result<double>> GetOpmOffsetAsync(int slot, int channel)
        {
            if (!IsConnected) 
                return Result<double>.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:CHANnel{channel}:OFFSET?";
                var res = await tcpClient.SendAndReceiveAsync(command);
                if (double.TryParse(res, out var offset))
                    return Result<double>.Success(offset);
                return Result<double>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
            }
            catch (Exception e)
            {
                logger.Error(e.Message);
                return Result<double>.Fail(ResultCode.Fail, e.Message, e);
            }
        }

        #endregion
    }
}