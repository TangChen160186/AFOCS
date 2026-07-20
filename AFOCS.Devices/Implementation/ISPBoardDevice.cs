using System.ComponentModel.Composition;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation
{
    /// <summary>
    /// ISP Board 配置
    /// </summary>
    public class ISPBoardConfig
    {
        /// <summary>产品配置文件路径</summary>
        public string ProductCfgFilePath { get; set; } = "ProductCfg.json";
    }

    /// <summary>
    /// ISP Board 设备实现 —— 通过 ISPBoard.dll (C++ wrapper) 与 NI-VISA 硬件通信。
    /// 使用传统 C 风格 P/Invoke：调用者分配缓冲区，out 参数返回长度，int 返回值=错误码。
    /// </summary>
    [Export]
    [Export(typeof(IIspBoardDevice))]
    [method: ImportingConstructor]
    public class ISPBoardDevice(IConfigService configService, ILogger logger) : IIspBoardDevice
    {
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _initialized;
        private const int ErrBufSize = 256;

        public bool IsConnected => _initialized;

        // ====================================================================
        // IDevice 接口
        // ====================================================================

        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            var config = await configService.LoadAsync<ISPBoardConfig>();
            config ??= new ISPBoardConfig();
            await configService.SaveAsync(config);

            var r = await InitializeAsync(config.ProductCfgFilePath, token);
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

                IntPtr appBuf = IntPtr.Zero, visaBuf = IntPtr.Zero, errBuf = IntPtr.Zero;
                try
                {
                    errBuf = ISPBoardNative.AllocBuf(ErrBufSize);

                    // 第一阶段：查询所需缓冲区大小
                    int ret = ISPBoardNative.ISP_Initialize(
                        productCfgFilePath,
                        IntPtr.Zero, 0, out int appLen,
                        IntPtr.Zero, 0, out int visaLen,
                        errBuf, ErrBufSize, out int errLen);

                    string? err = ISPBoardNative.CheckError(ret, errBuf, ErrBufSize, errLen);
                    if (err != null)
                        return Result<IspInitResult>.Fail(err);

                    // 第二阶段：分配实际缓冲区并获取数据
                    appBuf = ISPBoardNative.AllocBuf(appLen);
                    visaBuf = ISPBoardNative.AllocBuf(visaLen);

                    ret = ISPBoardNative.ISP_Initialize(
                        productCfgFilePath,
                        appBuf, appLen, out appLen,
                        visaBuf, visaLen, out visaLen,
                        errBuf, ErrBufSize, out errLen);

                    err = ISPBoardNative.CheckError(ret, errBuf, ErrBufSize, errLen);
                    if (err != null)
                        return Result<IspInitResult>.Fail(err);

                    var appNames = ISPBoardNative.ReadMultiStr(appBuf, appLen);
                    var visas = ISPBoardNative.ReadMultiStr(visaBuf, visaLen);

                    _initialized = true;
                    logger.Information($"ISP Board 初始化成功，应用: [{string.Join(", ", appNames)}]，VISA: [{string.Join(", ", visas)}]");

                    return Result<IspInitResult>.Success(new IspInitResult
                    {
                        AppNames = appNames,
                        DeviceVisaAddresses = visas
                    });
                }
                finally
                {
                    ISPBoardNative.FreeBuf(appBuf);
                    ISPBoardNative.FreeBuf(visaBuf);
                    ISPBoardNative.FreeBuf(errBuf);
                }
            }
            catch (DllNotFoundException ex)
            {
                logger.Error(ex, "ISPBoard.dll 未找到");
                return Result<IspInitResult>.Fail("ISPBoard.dll 未找到", ex);
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
        // 工程模式
        // ====================================================================

        public async Task<Result<byte[]>> SetEngineeringModeAsync(uint devIndex, bool enterEng)
        {
            if (!_initialized) return Result<byte[]>.Fail("设备未初始化");

            await _lock.WaitAsync().ConfigureAwait(false);
            IntPtr statusBuf = IntPtr.Zero, errBuf = IntPtr.Zero;
            try
            {
                errBuf = ISPBoardNative.AllocBuf(ErrBufSize);

                // 第一阶段：查询大小
                int ret = ISPBoardNative.ISP_EnterEngMode(
                    devIndex, enterEng ? (byte)1 : (byte)0,
                    IntPtr.Zero, 0, out int statusLen,
                    errBuf, ErrBufSize, out int errLen);
                string? err = ISPBoardNative.CheckError(ret, errBuf, ErrBufSize, errLen);
                if (err != null) return Result<byte[]>.Fail(err);

                // 第二阶段：获取数据
                statusBuf = ISPBoardNative.AllocBuf(statusLen);
                ret = ISPBoardNative.ISP_EnterEngMode(
                    devIndex, enterEng ? (byte)1 : (byte)0,
                    statusBuf, statusLen, out statusLen,
                    errBuf, ErrBufSize, out errLen);
                err = ISPBoardNative.CheckError(ret, errBuf, ErrBufSize, errLen);
                if (err != null) return Result<byte[]>.Fail(err);

                var status = ISPBoardNative.ReadByteArray(statusBuf, statusLen);
                logger.Information($"ISP Board 设备{devIndex} {(enterEng ? "进入" : "退出")}工程模式");
                return Result<byte[]>.Success(status);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "工程模式操作异常");
                return Result<byte[]>.Fail(ex.Message, ex);
            }
            finally
            {
                ISPBoardNative.FreeBuf(statusBuf);
                ISPBoardNative.FreeBuf(errBuf);
                _lock.Release();
            }
        }

        // ====================================================================
        // DUT 寄存器读写
        // ====================================================================

        public async Task<Result<ushort[]>> DutReadWriteAsync(
            uint devIndex, byte dutSlot, byte dutChannel,
            string appName, ushort operation, ushort[]? dataIn = null)
        {
            if (!_initialized) return Result<ushort[]>.Fail("设备未初始化");

            await _lock.WaitAsync().ConfigureAwait(false);
            IntPtr dataInPtr = IntPtr.Zero, dataOutBuf = IntPtr.Zero, errBuf = IntPtr.Zero;
            try
            {
                errBuf = ISPBoardNative.AllocBuf(ErrBufSize);
                (dataInPtr, int dataInLen) = ISPBoardNative.AllocUInt16Buf(dataIn);

                // 第一阶段：查询输出大小
                int ret = ISPBoardNative.ISP_DutReadWrite(
                    devIndex, dutSlot, dutChannel, appName, operation,
                    dataInPtr, dataInLen,
                    IntPtr.Zero, 0, out int dataOutLen,
                    errBuf, ErrBufSize, out int errLen);
                string? err = ISPBoardNative.CheckError(ret, errBuf, ErrBufSize, errLen);
                if (err != null) return Result<ushort[]>.Fail(err);

                // 第二阶段：获取数据
                dataOutBuf = ISPBoardNative.AllocBuf(dataOutLen * 2);
                ret = ISPBoardNative.ISP_DutReadWrite(
                    devIndex, dutSlot, dutChannel, appName, operation,
                    dataInPtr, dataInLen,
                    dataOutBuf, dataOutLen, out dataOutLen,
                    errBuf, ErrBufSize, out errLen);
                err = ISPBoardNative.CheckError(ret, errBuf, ErrBufSize, errLen);
                if (err != null) return Result<ushort[]>.Fail(err);

                var result = ISPBoardNative.ReadUInt16Array(dataOutBuf, dataOutLen);
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
                ISPBoardNative.FreeBuf(dataInPtr);
                ISPBoardNative.FreeBuf(dataOutBuf);
                ISPBoardNative.FreeBuf(errBuf);
                _lock.Release();
            }
        }

        // ====================================================================
        // 加热器扫描
        // ====================================================================

        public async Task<Result<(ushort[] MpdOutADC, ushort[] MpdInADC)>> HeaterScanAsync(
            uint devIndex, byte dutSlot, byte dutChannel,
            string appName, ushort[] dataIn)
        {
            if (!_initialized) return Result<(ushort[], ushort[])>.Fail("设备未初始化");

            await _lock.WaitAsync().ConfigureAwait(false);
            IntPtr dataInPtr = IntPtr.Zero, mpdOutBuf = IntPtr.Zero, mpdInBuf = IntPtr.Zero, errBuf = IntPtr.Zero;
            try
            {
                errBuf = ISPBoardNative.AllocBuf(ErrBufSize);
                (dataInPtr, int dataInLen) = ISPBoardNative.AllocUInt16Buf(dataIn);

                // 第一阶段：查询输出大小
                int ret = ISPBoardNative.ISP_HeaterScan(
                    devIndex, dutSlot, dutChannel, appName,
                    dataInPtr, dataInLen,
                    IntPtr.Zero, 0, out int mpdOutLen,
                    IntPtr.Zero, 0, out int mpdInLen,
                    errBuf, ErrBufSize, out int errLen);
                string? err = ISPBoardNative.CheckError(ret, errBuf, ErrBufSize, errLen);
                if (err != null) return Result<(ushort[], ushort[])>.Fail(err);

                // 第二阶段：获取数据
                mpdOutBuf = ISPBoardNative.AllocBuf(mpdOutLen * 2);
                mpdInBuf = ISPBoardNative.AllocBuf(mpdInLen * 2);
                ret = ISPBoardNative.ISP_HeaterScan(
                    devIndex, dutSlot, dutChannel, appName,
                    dataInPtr, dataInLen,
                    mpdOutBuf, mpdOutLen, out mpdOutLen,
                    mpdInBuf, mpdInLen, out mpdInLen,
                    errBuf, ErrBufSize, out errLen);
                err = ISPBoardNative.CheckError(ret, errBuf, ErrBufSize, errLen);
                if (err != null) return Result<(ushort[], ushort[])>.Fail(err);

                var mpdOut = ISPBoardNative.ReadUInt16Array(mpdOutBuf, mpdOutLen);
                var mpdIn = ISPBoardNative.ReadUInt16Array(mpdInBuf, mpdInLen);
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
                ISPBoardNative.FreeBuf(dataInPtr);
                ISPBoardNative.FreeBuf(mpdOutBuf);
                ISPBoardNative.FreeBuf(mpdInBuf);
                ISPBoardNative.FreeBuf(errBuf);
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
