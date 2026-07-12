using AFOCS.App.Communication;
using AFOCS.App.Core;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation
{
    public class OpticalPowerMeter : IOpticalPowerMeter
    {
        public EDeviceType Type => EDeviceType.OpticalPowerMeter;

        private readonly ITcpClient _tcpClient;
        private readonly IConfigService _configService;
        private readonly ILogger<OpticalPowerMeter> _logger;
        public bool IsConnected => _tcpClient.IsConnected;

        public OpticalPowerMeter(ITcpClient tcpClient, IConfigService configService, ILogger<OpticalPowerMeter> logger)
        {
            _tcpClient = tcpClient;
            _configService = configService;
            _logger = logger;
        }

        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            var config = await _configService.LoadAsync<OpticalPowerMeterConfig>();
            if (config == null)
            {
                config = OpticalPowerMeterConfig.Default;
                await _configService.SaveAsync(config);
            }

            TcpClientConfig tcpClientConfig = new TcpClientConfig
            {
                IpAddress = config.Ip,
                Port = config.Port,
            };
            var success = await _tcpClient.ConnectAsync(tcpClientConfig);
            if (success)
                return Result.Success("光功率计机箱初始化成功");
            

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

        public void Dispose()
        {
            _tcpClient.Dispose();
        }

        #region OS光源
        public async Task<Result<bool>> GetOsReadyAsync(int slot)
        {
            if (!IsConnected) 
                return Result<bool>.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:READY?";
                var res = await _tcpClient.SendAndReceiveAsync(command);

                if(string.IsNullOrWhiteSpace(res) || !res.Equals("1") || !res.Equals("0"))
                    return Result<bool>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
                return Result<bool>.Success(res.Equals("1"));
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
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
                var res = await _tcpClient.SendAndReceiveAsync(command);
                if (string.IsNullOrWhiteSpace(res) || !res.Equals("1") || !res.Equals("0"))
                    return Result<bool>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
                return Result<bool>.Success(res.Equals("1"));
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
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
                _logger.LogTrace($"发送指令:{command}");
                await _tcpClient.WriteLineAsync(command);
                return Result.Success();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
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

        public async Task<Result<double[]>> GetOsPowerAsync(int slot)
        {
            if (!IsConnected) 
                return Result<double[]>.Fail(ResultCode.Fail, "未连接设备");
            List<double> powers = new List<double>();
            try
            {
                string command = $":SLOT{slot}:POWer?";
                var res = await _tcpClient.SendAndReceiveAsync(command);
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
                _logger.LogError(e.Message);
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
                await _tcpClient.WriteLineAsync(command);
                return Result.Success();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
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
                var res = await _tcpClient.SendAndReceiveAsync(command);
                if (double.TryParse(res, out var offset))
                    return Result<double>.Success(offset);
                return Result<double>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return Result<double>.Fail(ResultCode.Fail, e.Message, e);
            }
        }

        #endregion
    }
}