using System.ComponentModel.Composition;
using AFOCS.App.Core;
using AFOCS.App.Shared;
using NationalInstruments.Visa;
using Serilog;

namespace AFOCS.App.Devices.Implementation
{
    public class ProgrammablePowerSupplyConfig
    {
        public string VisaAddress { get; set; } = "TCPIP0::127.0.0.1::inst0::INSTR";
        public int TimeoutMs { get; set; } = 3000;
    }

    [Export]
    [method: ImportingConstructor]
    public class ProgrammablePowerSupply(IConfigService configService, ILogger logger) : IProgrammablePowerSupply
    {
        public bool IsConnected { get; private set; }
        private MessageBasedSession? _session;
        private ResourceManager? _resourceManager;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            var config = await configService.LoadAsync<ProgrammablePowerSupplyConfig>();
            if (config == null)
            {
                config = new ProgrammablePowerSupplyConfig();
                await configService.SaveAsync(config);
            }

            try
            {
                _resourceManager = new ResourceManager();
                _session = (MessageBasedSession)_resourceManager.Open(config.VisaAddress);
                _session.TimeoutMilliseconds = config.TimeoutMs;
                IsConnected = true;
                logger.Information($"可编程电源({config.VisaAddress})初始化成功");

                await SendCommandAsync("*CLS");
                var errorResult = await GetErrorStatusAsync();
                if (!errorResult.IsSuccess)
                {
                    logger.Warning($"设备错误状态: {errorResult.Message}");
                }
                return Result.Success($"可编程电源({config.VisaAddress})初始化成功");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"可编程电源初始化失败: {config.VisaAddress}");
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
                logger.Information("可编程电源已停止");
                return Result.Success();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "停止可编程电源失败");
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
                logger.Verbose($"发送查询指令:{command}");
                _session.RawIO.Write(command);
                var response = _session.RawIO.ReadString().Trim();
                logger.Verbose($"收到响应:{response}");
                return Result<string>.Success(response);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"发送查询指令失败:{command}");
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
                logger.Verbose($"发送指令:{command}");
                _session.RawIO.Write(command);
                return Result.Success();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"发送指令失败:{command}");
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