using AFOCS.App.Communication;
using AFOCS.App.Core;
using AFOCS.App.Enums;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation
{
    public class OpticalPowerMeter : IOpticalPowerMeter
    {
        public EDeviceType Type => EDeviceType.OpticalPowerMeter;
        public WorkPos WorkPos { get; }
        private readonly ITcpClient _tcpClient;
        private readonly IConfigService _configService;
        private readonly ILogger<OpticalPowerMeter> _logger;
        public bool IsConnected => _tcpClient.IsConnected;

        public OpticalPowerMeter(WorkPos workPos, ITcpClient tcpClient, IConfigService configService, ILogger<OpticalPowerMeter> logger)
        {
            WorkPos = workPos;
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
                IpAddress = WorkPos == WorkPos.Left ? config.LeftConfig.Ip : config.RightConfig.Ip,
                Port = WorkPos == WorkPos.Left ? config.LeftConfig.Port : config.RightConfig.Port,
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
                _logger.LogTrace($"发送指令:{command}");
                var res = await _tcpClient.SendAndReceiveAsync(command);
                if (res.Equals("1"))
                    return Result<bool>.Success(true);
                if (res.Equals("0"))
                    return Result<bool>.Success(false);
                return Result<bool>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return Result<bool>.Fail(ResultCode.Fail, e.Message, e);
            }
        }

        public async Task<Result<(OSType, int)>> GetOsInformationAsync(int slot)
        {
            if (!IsConnected) 
                return Result<(OSType, int)>.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:INFormation?";
                _logger.LogTrace($"发送指令:{command}");
                var res = await _tcpClient.SendAndReceiveAsync(command);
                var ss = res.Split(",");
                if (ss.Length != 2)
                    return Result<(OSType, int)>.Fail(ResultCode.Fail, "未知返回数据");
                if (!int.TryParse(ss[0], out var type) || !int.TryParse(ss[0], out var channelCount))
                    return Result<(OSType, int)>.Fail(ResultCode.Fail, "未知返回数据");
                return Result<(OSType, int)>.Success(((OSType)type, channelCount));
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return Result<(OSType, int)>.Fail(ResultCode.Fail, e.Message, e);
            }
        }

        public async Task<Result> SetOsStatusAsync(int slot, int channel, bool status)
        {
            if (!IsConnected) 
                return Result.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:CHANnel{channel}:STATus {(status ? 1 : 0)}";
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

        public async Task<Result<bool>> GetOsStatusAsync(int slot, int channel)
        {
            if (!IsConnected) 
                return Result<bool>.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:CHANnel{channel}:STATus?";
                _logger.LogTrace($"发送指令:{command}");
                var res = await _tcpClient.SendAndReceiveAsync(command);
                if (res.Equals("1"))
                    return Result<bool>.Success(true);
                if (res.Equals("0"))
                    return Result<bool>.Success(false);
                return Result<bool>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
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
                _logger.LogTrace($"发送指令:{command}");
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

        public async Task<Result<List<double>>> GetOsPowerAsync(int slot)
        {
            if (!IsConnected) return Result<List<double>>.Fail(ResultCode.Fail, "未连接设备");
            List<double> powers = new List<double>();
            try
            {
                string command = $":SLOT{slot}:POWer?";
                _logger.LogTrace($"发送指令:{command}");
                var res = await _tcpClient.SendAndReceiveAsync(command);
                var splits = res.Split(",");
                if (splits.Length < 1)
                    return Result<List<double>>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
                foreach (var split in splits)
                {
                    if (double.TryParse(res, out var power))
                        powers.Add(power);
                    else
                        return Result<List<double>>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
                }
                return Result<List<double>>.Success(powers);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return Result<List<double>>.Fail(ResultCode.Fail, e.Message, e);
            }
        }

        public async Task<Result<int>> GetOsWaveLengthAsync(int slot, int channel)
        {
            if (!IsConnected) return Result<int>.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:CHANnel{channel}:WAVelength?";
                _logger.LogTrace($"发送指令:{command}");
                var res = await _tcpClient.SendAndReceiveAsync(command);
                if (int.TryParse(res, out var wave))
                    return Result<int>.Success(wave);
                return Result<int>.Fail(ResultCode.Fail, $"未知返回数据:{res}");
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return Result<int>.Fail(ResultCode.Fail, e.Message, e);
            }
        }

        #endregion

        #region 功率计
        public Task<Result<bool>> GetOpmReadyAsync(int slot)
        {
            return GetOsReadyAsync(slot);
        }

        public Task<Result<int>> GetOpmWaveLengthAsync(int slot, int channel)
        {
            return GetOsWaveLengthAsync(slot, channel);
        }

        public async Task<Result> SetOpmWaveLengthAsync(int slot, int channel, int waveLength)
        {
            if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:CHANnel{channel}:WAVelength {waveLength}";
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

        public Task<Result<double>> GetOpmPowerAsync(int slot, int channel)
        {
            return GetOsPowerAsync(slot, channel);
        }

        public Task<Result<List<double>>> GetOpmPowerAsync(int slot)
        {
            return GetOsPowerAsync(slot);
        }

        public async Task<Result> SetOpmOffsetAsync(int slot, int channel, double offset)
        {
            if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:CHANnel{channel}:OFFSET {offset:F3}";
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

        public async Task<Result<double>> GetOpmOffsetAsync(int slot, int channel)
        {
            if (!IsConnected) 
                return Result<double>.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                string command = $":SLOT{slot}:CHANnel{channel}:OFFSET?";
                _logger.LogTrace($"发送指令:{command}");
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