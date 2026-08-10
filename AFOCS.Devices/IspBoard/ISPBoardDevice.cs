using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.IspBoard
{
    /// <summary>
    /// ISP Board 配置
    /// </summary>
    public class ISPBoardConfig
    {
        /// <summary>产品配置文件路径</summary>
        public string ProductCfgFilePath { get; set; } = "ProductCfg.json";
        //public string RxAdcAppName { get; set; } = "RxADC";
        //public string MpdInAppName { get; set; } = "MPDInADC";
        //public string MpdInCoeffName { get; set; } = "MPDInADCCoeff";
        //public string MpdOutAppName { get; set; } = "MPDOutADC";
        //public string MpdOutCoeffAppName { get; set; } = "MPDOutADCCoeff";
        //public string IpsnAppName { get; set; } = "IPSN";
        //public string RxAdcFormulaAppName { get; set; } = "RxADC_R";
    }

    /// <summary>
    /// ISP Board 设备实现 —— 通过 ISPBoard.dll (C++ wrapper) 与 NI-VISA 硬件通信。
    /// 内存约定：
    ///   - C++ 端用 CoTaskMemAlloc 分配的输出（errorInfo, appNames, deviceVisa），C# 用 Marshal.FreeCoTaskMem 释放。
    ///   - C# 端用 Marshal.AllocHGlobal 分配数值缓冲区（dataIn, dataOut 等），用完释放。
    /// </summary>
    [Export]
    [Export(typeof(IIspBoardDevice))]
    [method: ImportingConstructor]
    public class ISPBoardDevice(IConfigService configService, ILogger logger) : IIspBoardDevice
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
            var config = await configService.LoadAsync<ISPBoardConfig>();
            config ??= new ISPBoardConfig();
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

                ISPBoardNative.IspInterfaceInitialEx_c(
                    productCfgFilePath,
                    out IntPtr appNamesPtr,   out uint appNameCount,
                    out IntPtr deviceVisaPtr, out uint deviceVisaCount,
                    out IntPtr errPtr,        out ushort errSize);

                string? err = ISPBoardNative.ReadError(errPtr, errSize);
                if (err != null)
                    return Result<IspInitResult>.Fail(err);

                var appNames = ISPBoardNative.ReadStrArray(appNamesPtr, appNameCount);
                var visas = ISPBoardNative.ReadStrArray(deviceVisaPtr, deviceVisaCount);

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
                (dataInPtr, ushort dataInLen) = ISPBoardNative.AllocUInt16Buf(dataIn);
                dataOutPtr = ISPBoardNative.AllocHGlobal(dataOutCount * 2);

                ISPBoardNative.IspDutReadWriteEx(
                    devIndex, dutSlot, dutChannel, appName, operation,
                    dataInPtr, dataInLen,
                    dataOutPtr, dataOutCount,
                    out IntPtr errPtr, out ushort errSize);

                string? err = ISPBoardNative.ReadError(errPtr, errSize);
                if (err != null) return Result<ushort[]>.Fail(err);

                var result = ISPBoardNative.ReadUInt16Array(dataOutPtr, dataOutCount);
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
                ISPBoardNative.FreeHGlobal(dataInPtr);
                ISPBoardNative.FreeHGlobal(dataOutPtr);
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
                (dataInPtr, ushort dataInLen) = ISPBoardNative.AllocDoubleBuf(dataIn);

                ISPBoardNative.IspFormularCalc(
                    appName,
                    dataInPtr, dataInLen,
                    out double result,
                    out IntPtr errPtr, out ushort errSize);

                string? err = ISPBoardNative.ReadError(errPtr, errSize);
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
                ISPBoardNative.FreeHGlobal(dataInPtr);
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
                (dataInPtr, ushort dataInLen) = ISPBoardNative.AllocUInt16Buf(dataIn);
                mpdOutPtr = ISPBoardNative.AllocHGlobal(mpdOutCount * 2);
                mpdInPtr = ISPBoardNative.AllocHGlobal(mpdInCount * 2);

                ISPBoardNative.IspDutHeaterScanEx(
                    devIndex, dutSlot, dutChannel, appName,
                    dataInPtr, dataInLen,
                    mpdOutPtr, mpdOutCount,
                    mpdInPtr, mpdInCount,
                    out IntPtr errPtr, out ushort errSize);

                string? err = ISPBoardNative.ReadError(errPtr, errSize);
                if (err != null) return Result<(ushort[], ushort[])>.Fail(err);

                var mpdOut = ISPBoardNative.ReadUInt16Array(mpdOutPtr, mpdOutCount);
                var mpdIn = ISPBoardNative.ReadUInt16Array(mpdInPtr, mpdInCount);
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
                ISPBoardNative.FreeHGlobal(dataInPtr);
                ISPBoardNative.FreeHGlobal(mpdOutPtr);
                ISPBoardNative.FreeHGlobal(mpdInPtr);
                _lock.Release();
            }
        }

        public void Dispose()
        {
            _lock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
