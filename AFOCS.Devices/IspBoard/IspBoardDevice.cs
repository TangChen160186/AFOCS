using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Text;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.IspBoard;

/// <summary>
/// ISP Board 设备实现 —— 通过 ISPBoard.dll (C++ wrapper) 与 NI-VISA 硬件通信。
/// 单一设备实例，内部管理左右两个工位的轮询。
/// 内存约定：
///   - C++ 端用 CoTaskMemAlloc 分配的输出（errorInfo, appNames, deviceVisa），C# 用 Marshal.FreeCoTaskMem 释放。
///   - C# 端用 Marshal.AllocHGlobal 分配数值缓冲区（dataIn, dataOut 等），用完释放。
/// </summary>
[Export(typeof(IIspBoardDevice))]
[Description("ISP Board")]
public class IspBoardDevice : IIspBoardDevice
{
    private readonly IConfigService _configService;
    private readonly ILogger _logger;

    private CancellationTokenSource? _rspCts;
    private Task? _rspPollingTask;
    private bool _initialized;

    public bool IsConnected => _initialized;
    public WorkPos WorkPos => WorkPos.None;

    /// <inheritdoc/>
    public event EventHandler<RspData[]>? RspDataUpdated;

    /// <inheritdoc/>
    public event EventHandler<MpdData[]>? MpdDataUpdated;

    /// <inheritdoc/>
    public event EventHandler<IpsnData>? IpsnDataUpdated;

    [ImportingConstructor]
    public IspBoardDevice(IConfigService configService, ILogger logger)
    {
        _configService = configService;
        _logger = logger;
    }

    // ====================================================================
    // IDevice 接口
    // ====================================================================

    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        var config = await _configService.LoadAsync<IspBoardConfig>();
        config ??= new IspBoardConfig();
        await _configService.SaveAsync(config);
        var r = await InitializeAsync(config.ProductCfgFilePath, token);

        return r.IsSuccess
            ? Result.Success(r.Message)
            : Result.Fail(r.Code, r.Message, r.Exception);
    }

    public async Task<Result<IspInitResult>> InitializeAsync(string productCfgFilePath, CancellationToken token = default)
    {
        if (_initialized)
        {
            _logger.Warning("ISP Board 已初始化");
            return Result<IspInitResult>.Fail("设备已初始化");
        }

        Result<IspInitResult> result;

        try
        {
            IspBoardNative.IspInterfaceInitialEx_c(
                productCfgFilePath,
                out IntPtr appNamesPtr, out uint appNameCount,
                out IntPtr deviceVisaPtr, out uint deviceVisaCount,
                out IntPtr errPtr, out ushort errSize);

            string? err = IspBoardNative.ReadError(errPtr, errSize);
            if (err != null)
                return Result<IspInitResult>.Fail(err);

            var appNames = IspBoardNative.ReadStrArray(appNamesPtr, appNameCount);
            var visas = IspBoardNative.ReadStrArray(deviceVisaPtr, deviceVisaCount);
            _initialized = true;

            _logger.Information("ISP Board 初始化成功，应用: [{apps}]，VISA: [{visas}]",
                string.Join(", ", appNames), string.Join(", ", visas));

            result = Result<IspInitResult>.Success(new IspInitResult
            {
                AppNames = appNames,
                DeviceVisaAddresses = visas
            });
        }
        catch (DllNotFoundException ex)
        {
            _logger.Error(ex, "ISPBoard.dll 未找到");
            result = Result<IspInitResult>.Fail("ISPBoard.dll 未找到", ex);
        }
        catch (EntryPointNotFoundException ex)
        {
            _logger.Error(ex, "ISPBoard.dll 导出函数未找到");
            result = Result<IspInitResult>.Fail("ISPBoard.dll 接口不匹配", ex);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ISP Board 初始化异常");
            result = Result<IspInitResult>.Fail($"初始化异常: {ex.Message}", ex);
        }

        if (result.IsSuccess)
            await StartRspPollingAsync(token);

        return result;
    }

    public async Task<Result> StopAsync(CancellationToken token = default)
    {
        await StopRspPollingAsync();

        _initialized = false;
        _logger.Information("ISP Board 已停止");
        return Result.Success();
    }

    public async Task<Result> ReConnectAsync(CancellationToken token = default)
    {
        await StopAsync(token);
        return await InitializeAsync(token);
    }

    // ====================================================================
    // DUT 寄存器读写
    // ====================================================================

    public async Task<Result<ushort[]>> DutReadWriteAsync(
        uint devIndex, byte dutSlot, byte dutChannel,
        string appName, byte operation, ushort[]? dataIn = null, ushort dataOutCount = 256)
    {
        if (!_initialized) return Result<ushort[]>.Fail("设备未初始化");

        IntPtr dataInPtr = IntPtr.Zero, dataOutPtr = IntPtr.Zero;
        try
        {
            (dataInPtr, ushort dataInLen) = IspBoardNative.AllocUInt16Buf(dataIn);
            dataOutPtr = IspBoardNative.AllocHGlobal(dataOutCount * 2);
            ushort actualCount = dataOutCount;

            IspBoardNative.IspDutReadWriteEx(
                devIndex, dutSlot, dutChannel, appName, operation,
                dataInPtr, dataInLen,
                dataOutPtr, ref actualCount,
                out IntPtr errPtr, out ushort errSize);

            string? err = IspBoardNative.ReadError(errPtr, errSize);
            if (err != null) return Result<ushort[]>.Fail(err);

            var result = IspBoardNative.ReadUInt16Array(dataOutPtr, actualCount);
            return Result<ushort[]>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "DUT ReadWrite 异常");
            return Result<ushort[]>.Fail(ex.Message, ex);
        }
        finally
        {
            IspBoardNative.FreeHGlobal(dataInPtr);
            IspBoardNative.FreeHGlobal(dataOutPtr);
        }
    }

    // ====================================================================
    // 公式计算
    // ====================================================================

    public async Task<Result<double>> FormularCalcAsync(string appName, double[] dataIn)
    {
        if (!_initialized) return Result<double>.Fail("设备未初始化");

        IntPtr dataInPtr = IntPtr.Zero;
        try
        {
            (dataInPtr, ushort dataInLen) = IspBoardNative.AllocDoubleBuf(dataIn);

            IspBoardNative.IspFormularCalc(
                appName,
                dataInPtr, dataInLen,
                out double result,
                out IntPtr errPtr, out ushort errSize);

            string? err = IspBoardNative.ReadError(errPtr, errSize);
            if (err != null) return Result<double>.Fail(err);

            return Result<double>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "FormularCalc 异常");
            return Result<double>.Fail(ex.Message, ex);
        }
        finally
        {
            IspBoardNative.FreeHGlobal(dataInPtr);
        }
    }

    // ====================================================================
    // 加热器扫描
    // ====================================================================

    public async Task<Result<(ushort[] MpdOutADC, ushort[] MpdInADC)>> HeaterScanAsync(
        uint devIndex, byte dutSlot, byte dutChannel,
        string appName, ushort[] dataIn, ushort mpdOutCount = 256, ushort mpdInCount = 256)
    {
        if (!_initialized) return Result<(ushort[], ushort[])>.Fail("设备未初始化");

        IntPtr dataInPtr = IntPtr.Zero, mpdOutPtr = IntPtr.Zero, mpdInPtr = IntPtr.Zero;
        try
        {
            (dataInPtr, ushort dataInLen) = IspBoardNative.AllocUInt16Buf(dataIn);
            mpdOutPtr = IspBoardNative.AllocHGlobal(mpdOutCount * 2);
            mpdInPtr = IspBoardNative.AllocHGlobal(mpdInCount * 2);
            ushort actualMpdOutCount = mpdOutCount;
            ushort actualMpdInCount = mpdInCount;

            IspBoardNative.IspDutHeaterScanEx(
                devIndex, dutSlot, dutChannel, appName,
                dataInPtr, dataInLen,
                mpdOutPtr, ref actualMpdOutCount,
                mpdInPtr, ref actualMpdInCount,
                out IntPtr errPtr, out ushort errSize);

            string? err = IspBoardNative.ReadError(errPtr, errSize);
            if (err != null) return Result<(ushort[], ushort[])>.Fail(err);

            var mpdOut = IspBoardNative.ReadUInt16Array(mpdOutPtr, actualMpdOutCount);
            var mpdIn = IspBoardNative.ReadUInt16Array(mpdInPtr, actualMpdInCount);
            return Result<(ushort[], ushort[])>.Success((mpdOut, mpdIn));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "HeaterScan 异常");
            return Result<(ushort[], ushort[])>.Fail(ex.Message, ex);
        }
        finally
        {
            IspBoardNative.FreeHGlobal(dataInPtr);
            IspBoardNative.FreeHGlobal(mpdOutPtr);
            IspBoardNative.FreeHGlobal(mpdInPtr);
        }
    }

    // ====================================================================
    // 实时读取
    // ====================================================================

    public async Task<Result<RspData[]>> ReadRspAsync(WorkPos workPos, CancellationToken token = default)
    {
        if (!_initialized) return Result<RspData[]>.Fail("设备未初始化");

        try
        {
            var config = await _configService.LoadAsync<IspBoardConfig>() ?? new IspBoardConfig();
            var ws = workPos == WorkPos.Left ? config.Left : config.Right;

            var channelLight = ws.ChannelLight ?? [];
            int chCount = channelLight.Length;
            if (chCount == 0)
                return Result<RspData[]>.Success([]);

            ushort dataOutCount = (ushort)(chCount * 2);
            var dev = (uint)ws.DeviceId;
            var slot = (byte)ws.DutSlot;
            var ch = (byte)ws.DutChannel;

            // 只读取 RxADC 一路，避免额外的 MPD_IN/MPD_OUT/IPSN 读取
            var rxResult = await DutReadWriteAsync(dev, slot, ch,
                config.RxAdcAppName, operation: 0, dataIn: null, dataOutCount: dataOutCount);
            var rxOk = rxResult.IsSuccess && rxResult.Data.Length >= chCount * 2;

            var rspArray = new RspData[chCount];
            for (int i = 0; i < chCount; i++)
            {
                double rsp = 0;
                if (rxOk)
                {
                    double rxAdc = CombineHighLow(rxResult.Data[i * 2], rxResult.Data[i * 2 + 1]);
                    var calc = await FormularCalcAsync(config.RxAdcFormulaAppName, [rxAdc, channelLight[i]]);
                    if (calc.IsSuccess) rsp = calc.Data;
                }
                rspArray[i] = new RspData(workPos, i, rsp);
            }

            return Result<RspData[]>.Success(rspArray);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "实时读取 RSP 异常");
            return Result<RspData[]>.Fail(ex.Message, ex);
        }
    }

    // ====================================================================
    // RSP 轮询
    // ====================================================================

    private async Task StartRspPollingAsync(CancellationToken token = default)
    {
        await StopRspPollingAsync();

        _rspCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var ct = _rspCts.Token;

        _rspPollingTask = Task.Run(async () =>
        {
            var config = await _configService.LoadAsync<IspBoardConfig>();
            var interval = TimeSpan.FromMilliseconds(config?.RspPollingIntervalMs ?? 200);
            _logger.Information("ISP Board RSP 轮询已启动，间隔 {interval}ms", interval.TotalMilliseconds);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var (rspData, mpdData, ipsnDatas) = await PollRspAsync(config!, ct);
                    if (rspData.Length > 0)
                        RspDataUpdated?.Invoke(this, rspData);
                    if (mpdData.Length > 0)
                        MpdDataUpdated?.Invoke(this, mpdData);
                    foreach (var ipsn in ipsnDatas)
                        IpsnDataUpdated?.Invoke(this, ipsn);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "ISP Board RSP 轮询异常");
                }

                try
                {
                    await Task.Delay(interval, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.Information("ISP Board RSP 轮询已停止");
        }, ct);
    }

    private async Task StopRspPollingAsync()
    {
        if (_rspCts != null)
        {
            await _rspCts.CancelAsync();
            _rspCts.Dispose();
            _rspCts = null;
        }

        if (_rspPollingTask != null)
        {
            try { await _rspPollingTask; } catch (OperationCanceledException) { }
            _rspPollingTask = null;
        }
    }

    /// <summary>对单个工位执行一次完整的 RSP 数据读取</summary>
    private async Task<(RspData[] Rsp, MpdData[] Mpd, IpsnData Ipsn)> PollWorkstationAsync(
        WorkPos workPos, WorkstationConfig ws, IspBoardConfig config, CancellationToken ct)
    {
        var channelLight = ws.ChannelLight ?? [];
        int chCount = channelLight.Length;
        if (chCount == 0)
            return ([], [], new IpsnData(workPos, ""));

        ushort dataOutCount = (ushort)(chCount * 2);
        var dev = (uint)ws.DeviceId;
        var slot = (byte)ws.DutSlot;
        var ch = (byte)ws.DutChannel;

        // 4 路并行读取
        var rxTask = DutReadWriteAsync(dev, slot, ch,
            config.RxAdcAppName, operation: 0, dataIn: null, dataOutCount: dataOutCount);
        var mpdInTask = DutReadWriteAsync(dev, slot, ch,
            config.MpdInAppName, operation: 0, dataIn: null, dataOutCount: dataOutCount);
        var mpdOutTask = DutReadWriteAsync(dev, slot, ch,
            config.MpdOutAppName, operation: 0, dataIn: null, dataOutCount: dataOutCount);
        var ipsnTask = DutReadWriteAsync(dev, slot, ch,
            config.IpsnAppName, operation: 0, dataIn: null, dataOutCount: 256);

        await Task.WhenAll(rxTask, mpdInTask, mpdOutTask, ipsnTask);

        var rxResult = rxTask.Result;
        var mpdInResult = mpdInTask.Result;
        var mpdOutResult = mpdOutTask.Result;
        var ipsnResult = ipsnTask.Result;

        var rxOk = rxResult.IsSuccess && rxResult.Data.Length >= chCount * 2;
        var mpdInOk = mpdInResult.IsSuccess && mpdInResult.Data.Length >= chCount * 2;
        var mpdOutOk = mpdOutResult.IsSuccess && mpdOutResult.Data.Length >= chCount * 2;

        // 每通道并行计算 RSP（FormularCalc）
        var calcTasks = new Task<(RspData Rsp, MpdData Mpd)>[chCount];
        for (int i = 0; i < chCount; i++)
        {
            int idx = i; // capture
            calcTasks[i] = CalcChannelAsync(idx);
        }
        var results = await Task.WhenAll(calcTasks);

        // IPSN: ushort[] → ASCII 字符串
        string ipsnText = "";
        if (ipsnResult.IsSuccess && ipsnResult.Data.Length > 0)
        {
            ipsnText = Encoding.ASCII.GetString(
                ipsnResult.Data.Select(v => (byte)(v & 0xFF)).ToArray()
            ).TrimEnd('\0');
        }

        _logger.Debug("ISP Board {pos} 轮询完成: Chs={chs}, RSP/MPD ok={rx}/{mi}/{mo}, IPSN=\"{ipsn}\"",
            workPos, chCount, rxOk, mpdInOk, mpdOutOk, ipsnText);

        var rspArray = results.Select(r => r.Rsp).ToArray();
        var mpdArray = results.Select(r => r.Mpd).ToArray();
        return (rspArray, mpdArray, new IpsnData(workPos, ipsnText));

        // 单通道计算
        async Task<(RspData Rsp, MpdData Mpd)> CalcChannelAsync(int i)
        {
            ct.ThrowIfCancellationRequested();

            double? rsp = null;
            if (rxOk)
            {
                double rxAdc = CombineHighLow(rxResult.Data[i * 2], rxResult.Data[i * 2 + 1]);
                var calc = await FormularCalcAsync(config.RxAdcFormulaAppName, [rxAdc, channelLight[i]]);
                if (calc.IsSuccess) rsp = calc.Data;
            }

            double mpdIn = mpdInOk ? CombineHighLow(mpdInResult.Data[i * 2], mpdInResult.Data[i * 2 + 1]) : 0;
            double mpdOut = mpdOutOk ? CombineHighLow(mpdOutResult.Data[i * 2], mpdOutResult.Data[i * 2 + 1]) : 0;

            return (new RspData(workPos, i, rsp ?? 0), new MpdData(workPos, i, mpdIn, mpdOut));
        }
    }

    /// <summary>执行一次轮询：左右工位并行读取</summary>
    private async Task<(RspData[], MpdData[], IpsnData[])> PollRspAsync(IspBoardConfig config, CancellationToken ct)
    {
        var leftTask = PollWorkstationAsync(WorkPos.Left, config.Left, config, ct);
        var rightTask = PollWorkstationAsync(WorkPos.Right, config.Right, config, ct);

        await Task.WhenAll(leftTask, rightTask);

        var left = leftTask.Result;
        var right = rightTask.Result;

        var allRsp = left.Rsp.Concat(right.Rsp).ToArray();
        var allMpd = left.Mpd.Concat(right.Mpd).ToArray();
        var allIpsn = new[] { left.Ipsn, right.Ipsn };

        return (allRsp, allMpd, allIpsn);
    }

    /// <summary>合并高低字节为一个 16 位无符号值</summary>
    private static double CombineHighLow(ushort high, ushort low)
        => ((byte)(high & 0xFF) << 8) | (byte)(low & 0xFF);

    public void Dispose()
    {
        _rspCts?.Cancel();
        _rspCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
