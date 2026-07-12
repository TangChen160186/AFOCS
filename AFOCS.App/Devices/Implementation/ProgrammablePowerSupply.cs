using AFOCS.App.Core;
using AFOCS.App.Enums;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;
using NationalInstruments.Visa;

namespace AFOCS.App.Devices.Implementation
{
    public class ProgrammablePowerSupply : IProgrammablePowerSupply
    {
        public bool IsConnected { get; private set; }
        public EDeviceType Type => EDeviceType.ProgrammablePowerSupply;
        public WorkPos WorkPos => WorkPos.Common;

        private MessageBasedSession? _session;
        private ResourceManager? _resourceManager;
        private readonly IConfigService _configService;
        private readonly ILogger<ProgrammablePowerSupply> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public ProgrammablePowerSupply(IConfigService configService, ILogger<ProgrammablePowerSupply> logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            var config = await _configService.LoadAsync<ProgrammablePowerSupplyConfig>();
            if (config == null)
            {
                config = ProgrammablePowerSupplyConfig.Default;
                await _configService.SaveAsync(config);
            }

            try
            {
                _resourceManager = new ResourceManager();
                _session = (MessageBasedSession)_resourceManager.Open(config.VisaAddress);
                _session.TimeoutMilliseconds = config.TimeoutMs;
                IsConnected = true;
                _logger.LogInformation($"可编程电源({config.VisaAddress})初始化成功");

                await SendCommandAsync("*CLS");
                var errorResult = await GetErrorStatusAsync();
                if (!errorResult.IsSuccess)
                {
                    _logger.LogWarning($"设备错误状态: {errorResult.Message}");
                }
                return Result.Success($"可编程电源({config.VisaAddress})初始化成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"可编程电源初始化失败: {config.VisaAddress}");
                CleanupConnection();
                return Result.Fail(ResultCode.Fail, ex.Message, ex);
            }
        }

        public async Task<Result> StopAsync(CancellationToken token = default)
        {
            if (!IsConnected) return Result.Fail(ResultCode.Fail, "未连接设备");
            try
            {
                CleanupConnection();
                IsConnected = false;
                _logger.LogInformation("可编程电源已停止");
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止可编程电源失败");
                return Result.Fail(ResultCode.Fail, ex.Message, ex);
            }
        }

        public async Task<Result> ReConnectAsync(CancellationToken token = default)
        {
            await StopAsync(token);
            return await InitializeAsync(token);
        }

        public void Dispose()
        {
            CleanupConnection();
            _lock.Dispose();
        }

        private void CleanupConnection()
        {
            _session?.Dispose();
            _session = null;
            _resourceManager?.Dispose();
            _resourceManager = null;
        }

        public static string[] GetAvailableResources()
        {
            try
            {
                using var rm = new ResourceManager();
                return rm.Find("?*INSTR").ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public async Task<Result> SetChannelStatusAsync(int channel, bool status)
        {
            return await SendCommandAsync($"OUTPut CH{channel},{(status ? "ON" : "OFF")}");
        }

        public async Task<Result<bool>> GetChannelStatusAsync(int channel)
        {
            var result = await SendQueryAsync($"OUTP? CH{channel}");
            if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Data))
                return Result<bool>.Fail(result.Code, result.Message);
            return Result<bool>.Success(result.Data.ToLower().Equals("on"));
        }

        public async Task<Result> SetVoltageAndCurrentAsync(int channel, double voltage, double current)
        {
            return await SendCommandAsync($"APPL CH{channel.ToString()},{voltage},{current}");
        }

        public async Task<Result<(double, double)>> GetVoltageAndCurrentAsync(int channel)
        {
            var result = await SendQueryAsync($"APPL? CH{channel}");
            if (!result.IsSuccess) return Result<(double, double)>.Fail(result.Code, result.Message);
            var datas = result.Data?.Split(",");

            if (datas != null && datas.Length == 3)
            {
                if (double.TryParse(datas[1], out double voltage) && double.TryParse(datas[2], out double current))
                {
                    return Result<(double, double)>.Success((voltage, current));
                }
            }

            return Result<(double, double)>.Fail(ResultCode.Fail, $"未知返回数据:{result.Data}");
        }

        public async Task<Result<string>> GetErrorStatusAsync()
        {
            var result = await SendQueryAsync("SYSTem:ERRor?");
            if (!result.IsSuccess) return Result<string>.Fail(result.Code, result.Message);

            var errorText = result.Data?.Trim();
            if (errorText?.StartsWith("+0") == true || errorText?.StartsWith("0") == true)
            {
                return Result<string>.Success("无错误");
            }
            return Result<string>.Fail(ResultCode.Fail, $"设备错误: {errorText}");
        }

        private async Task<Result<string>> SendQueryAsync(string command)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!IsConnected || _session == null)
                {
                    return Result<string>.Fail(ResultCode.Fail, "未连接设备");
                }

                command += Environment.NewLine;
                _logger.LogTrace($"发送查询指令:{command}");
                _session.RawIO.Write(command);
                var response = _session.RawIO.ReadString().Trim();
                _logger.LogTrace($"收到响应:{response}");
                return Result<string>.Success(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"发送查询指令失败:{command}");
                HandleConnectionError();
                return Result<string>.Fail(ResultCode.Fail, ex.Message, ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<Result> SendCommandAsync(string command)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!IsConnected || _session == null)
                {
                    return Result.Fail(ResultCode.Fail, "未连接设备");
                }

                command += Environment.NewLine;
                _logger.LogTrace($"发送指令:{command}");
                _session.RawIO.Write(command);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"发送指令失败:{command}");
                HandleConnectionError();
                return Result.Fail(ResultCode.Fail, ex.Message, ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        private void HandleConnectionError()
        {
            try
            {
                IsConnected = false;
                CleanupConnection();
            }
            catch { }
        }
    }
}