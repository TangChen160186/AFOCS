using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.IspBoard;

/// <summary>
/// ISP Board 设备实现 —— 通过 ISPBoard.dll (C++ wrapper) 与 NI-VISA 硬件通信。
/// 内存约定：
///   - C++ 端用 CoTaskMemAlloc 分配的输出（errorInfo, appNames, deviceVisa），C# 用 Marshal.FreeCoTaskMem 释放。
///   - C# 端用 Marshal.AllocHGlobal 分配数值缓冲区（dataIn, dataOut 等），用完释放。
/// </summary>
[Export]
[Export(typeof(IIspBoardDevice))]
[Description("ISP Board")]
[method: ImportingConstructor]
public class IspBoardDeviceBase(IConfigService configService, ILogger logger) : IIspBoardDevice
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;

    public bool IsConnected => _initialized;
    public WorkPos WorkPos { get; }

    // ====================================================================
    // IDevice 接口
    // ====================================================================
        
        
    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        var config = await configService.LoadAsync<IspBoardConfig>();
        config ??= new IspBoardConfig();
        await configService.SaveAsync(config);
        var r = await InitializeAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), config.ProductCfgFilePath), token);

        return r.IsSuccess
            ? Result.Success(r.Message)
            : Result.Fail(r.Code, r.Message, r.Exception);
    }


    public async Task<Result<IspInitResult>> InitializeAsync(string productCfgFilePath, CancellationToken token = default)
    {
        await _lock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                logger.Warning("ISP Board 已初始化");
                return Result<IspInitResult>.Fail("设备已初始化");
            }

            IspBoardNative.IspInterfaceInitialEx_c(
                productCfgFilePath,
                out IntPtr appNamesPtr,   out uint appNameCount,
                out IntPtr deviceVisaPtr, out uint deviceVisaCount,
                out IntPtr errPtr,        out ushort errSize);

            string? err = IspBoardNative.ReadError(errPtr, errSize);
            if (err != null)
                return Result<IspInitResult>.Fail(err);

            var appNames = IspBoardNative.ReadStrArray(appNamesPtr, appNameCount);
            var visas = IspBoardNative.ReadStrArray(deviceVisaPtr, deviceVisaCount);

            _initialized = true;
            logger.Information($"ISP Board 初始化成功，应用: [{string.Join(", ", appNames)}]，VISA: [{string.Join(", ", visas)}]");

            return Result<IspInitResult>.Success(new IspInitResult
            {
                AppNames = appNames,
                DeviceVisaAddresses = visas
            });
        }
        catch (DllNotFoundException ex)
        {
            logger.Error(ex, "ISPBoard.dll 未找到");
            return Result<IspInitResult>.Fail("ISPBoard.dll 未找到", ex);
        }
        catch (EntryPointNotFoundException ex)
        {
            logger.Error(ex, "ISPBoard.dll 导出函数未找到");
            return Result<IspInitResult>.Fail("ISPBoard.dll 接口不匹配", ex);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "ISP Board 初始化异常");
            return Result<IspInitResult>.Fail($"初始化异常: {ex.Message}", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> StopAsync(CancellationToken token = default)
    {
        await _lock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            _initialized = false;
            logger.Information("ISP Board 已停止");
            return Result.Success();
        }
        finally
        {
            _lock.Release();
        }
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

        await _lock.WaitAsync().ConfigureAwait(false);
        IntPtr dataInPtr = IntPtr.Zero, dataOutPtr = IntPtr.Zero;
        try
        {
            (dataInPtr, ushort dataInLen) = IspBoardNative.AllocUInt16Buf(dataIn);
            dataOutPtr = IspBoardNative.AllocHGlobal(dataOutCount * 2);

            IspBoardNative.IspDutReadWriteEx(
                devIndex, dutSlot, dutChannel, appName, operation,
                dataInPtr, dataInLen,
                dataOutPtr, dataOutCount,
                out IntPtr errPtr, out ushort errSize);

            string? err = IspBoardNative.ReadError(errPtr, errSize);
            if (err != null) return Result<ushort[]>.Fail(err);

            var result = IspBoardNative.ReadUInt16Array(dataOutPtr, dataOutCount);
            logger.Debug($"DUT ReadWrite (Dev:{devIndex}, Slot:{dutSlot}, Ch:{dutChannel}, Op:{operation}) -> {result.Length} 个值");
            return Result<ushort[]>.Success(result);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "DUT ReadWrite 异常");
            return Result<ushort[]>.Fail(ex.Message, ex);
        }
        finally
        {
            IspBoardNative.FreeHGlobal(dataInPtr);
            IspBoardNative.FreeHGlobal(dataOutPtr);
            _lock.Release();
        }
    }

    // ====================================================================
    // 公式计算
    // ====================================================================

    public async Task<Result<double>> FormularCalcAsync(string appName, double[] dataIn)
    {
        if (!_initialized) return Result<double>.Fail("设备未初始化");

        await _lock.WaitAsync().ConfigureAwait(false);
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

            logger.Debug($"FormularCalc (App:{appName}) -> {result}");
            return Result<double>.Success(result);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "FormularCalc 异常");
            return Result<double>.Fail(ex.Message, ex);
        }
        finally
        {
            IspBoardNative.FreeHGlobal(dataInPtr);
            _lock.Release();
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

        await _lock.WaitAsync().ConfigureAwait(false);
        IntPtr dataInPtr = IntPtr.Zero, mpdOutPtr = IntPtr.Zero, mpdInPtr = IntPtr.Zero;
        try
        {
            (dataInPtr, ushort dataInLen) = IspBoardNative.AllocUInt16Buf(dataIn);
            mpdOutPtr = IspBoardNative.AllocHGlobal(mpdOutCount * 2);
            mpdInPtr = IspBoardNative.AllocHGlobal(mpdInCount * 2);

            IspBoardNative.IspDutHeaterScanEx(
                devIndex, dutSlot, dutChannel, appName,
                dataInPtr, dataInLen,
                mpdOutPtr, mpdOutCount,
                mpdInPtr, mpdInCount,
                out IntPtr errPtr, out ushort errSize);

            string? err = IspBoardNative.ReadError(errPtr, errSize);
            if (err != null) return Result<(ushort[], ushort[])>.Fail(err);

            var mpdOut = IspBoardNative.ReadUInt16Array(mpdOutPtr, mpdOutCount);
            var mpdIn = IspBoardNative.ReadUInt16Array(mpdInPtr, mpdInCount);
            logger.Debug($"HeaterScan (Dev:{devIndex}, Slot:{dutSlot}, Ch:{dutChannel}) -> Out:{mpdOut.Length}, In:{mpdIn.Length}");
            return Result<(ushort[], ushort[])>.Success((mpdOut, mpdIn));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "HeaterScan 异常");
            return Result<(ushort[], ushort[])>.Fail(ex.Message, ex);
        }
        finally
        {
            IspBoardNative.FreeHGlobal(dataInPtr);
            IspBoardNative.FreeHGlobal(mpdOutPtr);
            IspBoardNative.FreeHGlobal(mpdInPtr);
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}