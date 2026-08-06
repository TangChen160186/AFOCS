using System.Runtime.InteropServices;
using AFOCS.Infrastructure;
using MvCamCtrl.NET;
using Serilog;
using static MvCamCtrl.NET.MyCamera;

namespace AFOCS.Devices.Implementation
{
    public class HkCameraConfig : ICloneable
    {
        public string ChSerialNumber { get; set; } = "ChSerialNumber";

        /// <summary>相机精度 (mm/pixel)，上相机 0.0023，侧相机 0.0018</summary>
        public virtual double Precision { get; set; } = 0;

        public HkCameraConfig Clone() => new()
        {
            ChSerialNumber = ChSerialNumber,
            Precision = Precision,
        };

        object ICloneable.Clone() => Clone();
    }

    public class Camera<T>(IConfigService configService, ILogger logger) : ICamera
        where T : HkCameraConfig, new()
    {
        private T _config = new();
        public uint Height { get; private set; }
        public uint Width { get; private set; }
        public uint WidthStep { get; private set; }
        public uint HeightStep { get; private set; }

        private readonly MyCamera _camera = new();
        private cbOutputExdelegate _outputCallback;

        // 缓存最新帧（从回调中拷贝）
        private readonly object _lastFrameLock = new();
        private byte[]? _lastFrameData;
        private int _lastFrameW, _lastFrameH;
        private bool _lastFrameIsMono;

        public bool IsConnected { get; private set; }

        public HkCameraConfig GetConfig() => _config.Clone();

        public async Task SaveConfigAsync(HkCameraConfig config)
        {
            var newConfig = new T { ChSerialNumber = config.ChSerialNumber ,Precision = config.Precision};
            _config = newConfig;
            await configService.SaveAsync(newConfig);
        }


        public event EventHandler<ImagePreviewedEventArgs>? ImageReceived;

        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            try
            {
                var config = await configService.LoadAsync<T>();
                if (config == null)
                {
                    config = new T();
                    await configService.SaveAsync(config);
                }
                _config = config;

                var deviceInfo = FindCameraByChSerialNumber(config.ChSerialNumber);
                if (deviceInfo == null)
                {
                    return Result.Fail(ResultCode.Fail, $"Find Device Error: Target camera 【{config.ChSerialNumber}】 not found");
                }
                

                var success = OpenDevice(deviceInfo.Value);
                if(!success) 
                    return Result.Fail(ResultCode.Fail, $"Open Device fail");
                IsConnected = true;
                InitImageSize();
                InitCameraParm();
                SetImageCallback();

                // 初始化后自动开始连续采集
                var ret = _camera.MV_CC_StartGrabbing_NET();
                if (ret != 0)
                    return Result.Fail(ResultCode.Fail, $"启动采集失败，错误码：{ret}");

                return Result.Success();

            }
            catch (Exception e)
            {
                return Result.Fail(ResultCode.Fail, $"{e}");
            }
        }

        private void InitImageSize()
        {
            MVCC_INTVALUE value = new MVCC_INTVALUE();
            var ret = _camera.MV_CC_GetWidth_NET(ref value);
            if (ret == 0)
            {
                Width = value.nCurValue;
                WidthStep = value.nInc;
            }
    
            ret = _camera.MV_CC_GetHeight_NET(ref value);
            if (ret == 0)
            {
                Height = value.nCurValue;
                HeightStep = value.nInc;
            }
        }

        private void InitCameraParm()
        {
            _camera.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);
            _camera.MV_CC_SetEnumValue_NET("TriggerSource", (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE);
            _camera.MV_CC_SetEnumValue_NET("AcquisitionMode", (uint)MyCamera.MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);

            _camera.MV_CC_SetEnumValue_NET("ExposureAuto", 0);
            _camera.MV_CC_SetFloatValue_NET("TriggerDelay", 0);
            _camera.MV_CC_SetBoolValue_NET("AcquisitionFrameRateEnable", false);
            _camera.MV_CC_SetBoolValue_NET("TriggerCacheEnable", false);
            _camera.MV_CC_SetEnumValue_NET("GainAuto", 0);
        }

        private void SetImageCallback()
        {
            _outputCallback = new cbOutputExdelegate(OnCameraImageCallback);
            var ret = _camera.MV_CC_RegisterImageCallBackEx_NET(_outputCallback, IntPtr.Zero);
        }

        private void OnCameraImageCallback(IntPtr pData, ref MV_FRAME_OUT_INFO_EX pFrameInfo, IntPtr pUser)
        {
            try
            {
                uint w = pFrameInfo.nWidth;
                uint h = pFrameInfo.nHeight;
                uint frameLen = pFrameInfo.nFrameLen;
                bool isMono = IsPixelMono(pFrameInfo.enPixelType);

                // 拷贝最新帧数据
                lock (_lastFrameLock)
                {
                    if (_lastFrameData == null || _lastFrameData.Length < frameLen)
                        _lastFrameData = new byte[frameLen];
                    Marshal.Copy(pData, _lastFrameData, 0, (int)frameLen);
                    _lastFrameW = (int)w;
                    _lastFrameH = (int)h;
                    _lastFrameIsMono = isMono;
                }

                ImageReceived?.Invoke(this, new ImagePreviewedEventArgs(pData, (int)w, (int)h, isMono));
            }
            catch (Exception ex)
            {
                logger.Error($"{ex}");
            }
        }

        /// <summary>
        /// 判断像素格式是否为单色（Mono 族）。
        /// </summary>
        private static bool IsPixelMono(MvGvspPixelType type) => ((uint)type & 0xFF000000) == 0x01000000;
        private bool OpenDevice(MV_CC_DEVICE_INFO deviceInfo)
        {
            try
            {
                int nRet = _camera.MV_CC_CreateDevice_NET(ref deviceInfo);
                if (nRet == 0)
                    nRet = _camera.MV_CC_OpenDevice_NET();
                else
                    return false;

                return nRet == 0;
            }
            catch (Exception e)
            {
                logger.Error($"error:OpenDevice:{e.Message}");
                return false;
            }

        }

        public unsafe MV_CC_DEVICE_INFO? FindCameraByChSerialNumber(string ch)
        {
            MyCamera.MV_CC_DEVICE_INFO_LIST deviceList = new();
            int ret = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE, ref deviceList);

            if (ret != 0)
            {
                logger.Error("枚举相机失败，错误码：{Code}", ret);
                return null;
            }

            if (deviceList.nDeviceNum == 0)
            {
                logger.Information("未扫描到任何网口相机");
                return null;
            }
            for (int i = 0; i < deviceList.nDeviceNum; i++)
            {
                var device = Marshal.PtrToStructure<MyCamera.MV_CC_DEVICE_INFO>(deviceList.pDeviceInfo[i]);

                if (device.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                {
                    byte[] arr = device.SpecialInfo.stGigEInfo;
                    fixed (byte* p = arr)
                    {
                        IntPtr ptr = (IntPtr)p;
                        var gigeInfo = Marshal.PtrToStructure<MyCamera.MV_GIGE_DEVICE_INFO>(ptr);
                        string sn = gigeInfo.chSerialNumber?.Trim() ?? string.Empty;
                        if (!string.IsNullOrEmpty(sn) && sn.Equals(ch))
                        {
                            logger.Debug("扫描到相机序列号：{SN}", sn);
                            return device;
                            
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取所有网口GigE相机的序列号列表
        /// </summary>
        /// <param name="logger">日志实例</param>
        /// <returns>序列号集合</returns>
        public static unsafe List<(string,string)> GetAllCameraSerialNumbers(ILogger logger)
        {
            List<(string,string)> snList = new();
            MyCamera.MV_CC_DEVICE_INFO_LIST deviceList = new();
            int ret = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE, ref deviceList);

            if (ret != 0)
            {
                logger.Error("枚举相机失败，错误码：{Code}", ret);
                return snList;
            }

            if (deviceList.nDeviceNum == 0)
            {
                logger.Information("未扫描到任何网口相机");
                return snList;
            }

            for (int i = 0; i < deviceList.nDeviceNum; i++)
            {
                var device = Marshal.PtrToStructure<MyCamera.MV_CC_DEVICE_INFO>(deviceList.pDeviceInfo[i]);

                if (device.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                {
                    byte[] arr = device.SpecialInfo.stGigEInfo;
                    fixed (byte* p = arr)
                    {
                        IntPtr ptr = (IntPtr)p;
                        var gigeInfo = Marshal.PtrToStructure<MyCamera.MV_GIGE_DEVICE_INFO>(ptr);
                        string sn = gigeInfo.chSerialNumber?.Trim() ?? string.Empty;
                        uint ip = gigeInfo.nCurrentIp;
                        if (!string.IsNullOrEmpty(sn))
                        {
                            snList.Add((sn,UIntToIpString(ip)));
                            logger.Debug("扫描到相机序列号：{SN}", sn);
                        }
                    }
                }
            }

            return snList;
        }

        /// <summary>
        /// 将 uint 类型的 IP 地址转换为标准的 IP 字符串
        /// </summary>
        /// <param name="ipUint">uint 类型的 IP 地址（如 0xC0A80164）</param>
        /// <returns>点分十进制格式的 IP 字符串（如 "192.168.1.100"）</returns>
        private static string UIntToIpString(uint ipUint)
        {
            // 方式一：使用 IPAddress 类（推荐，简洁明了）
            return $"{(ipUint >> 24) & 0xFF}." +
                   $"{(ipUint >> 16) & 0xFF}." +
                   $"{(ipUint >> 8) & 0xFF}." +
                   $"{ipUint & 0xFF}";
        }
        public async Task<Result> StopAsync(CancellationToken token = default)
        {
            if (!IsConnected)
                return Result.Fail(ResultCode.Fail, "未链接设备");
            Dispose();
            return Result.Success();
        }

        public async Task<Result> ReConnectAsync(CancellationToken token = default)
        {
            Dispose();
            return await InitializeAsync(token);
        }


        public async Task<Result> StartCameraAsync()
        {
            if (!IsConnected)
                return Result.Fail(ResultCode.Fail, "未链接设备");
            var ret = _camera.MV_CC_StartGrabbing_NET();

            if (ret != 0) 
                return Result.Fail(ResultCode.Fail, "捕获失败");
            return Result.Success();
        }
        public async Task<Result> SoftwareTriggerOnce()
        {
            var ret = _camera.MV_CC_TriggerSoftwareExecute_NET();
            if (ret != 0)
                return Result.Fail(ResultCode.Fail, "软件触发失败");
            return Result.Success();
        }

        public async Task<Result<string>> CaptureImageAsync(string filePath)
        {
            byte[]? frameData;
            int w, h;
            bool isMono;

            lock (_lastFrameLock)
            {
                if (_lastFrameData == null)
                    return Result<string>.Fail("暂无图像帧");

                frameData = new byte[_lastFrameData.Length];
                Array.Copy(_lastFrameData, frameData, _lastFrameData.Length);
                w = _lastFrameW;
                h = _lastFrameH;
                isMono = _lastFrameIsMono;
            }

            if (w == 0 || h == 0)
                return Result<string>.Fail("图像尺寸未初始化");

            if (isMono)
            {
                using var pinned = new PinnedArray(frameData);
                SaveAs8BitBmp(pinned.Ptr, (uint)w, (uint)h, filePath);
            }
            else
            {
                // 彩色相机回退到 SDK BGR 转换
                uint bgrSize = (uint)w * (uint)h * 3;
                IntPtr pBuf = Marshal.AllocHGlobal((int)bgrSize);
                try
                {
                    MV_FRAME_OUT_INFO_EX info = new();
                    int ret = _camera.MV_CC_GetImageForBGR_NET(pBuf, bgrSize, ref info, 5000);
                    if (ret != 0)
                        return Result<string>.Fail($"BGR 转换失败，错误码: {ret}");
                    SaveAs24BitBmp(pBuf, (uint)w, (uint)h, filePath);
                }
                finally { Marshal.FreeHGlobal(pBuf); }
            }

            return Result<string>.Success(filePath);
        }

        public Task<Result<(byte[] Data, int Width, int Height, bool IsMono)>> GrabFrameAsync()
        {
            lock (_lastFrameLock)
            {
                if (_lastFrameData == null)
                    return Task.FromResult(Result<(byte[], int, int, bool)>.Fail("暂无图像帧"));

                var copy = new byte[_lastFrameData.Length];
                Array.Copy(_lastFrameData, copy, _lastFrameData.Length);
                return Task.FromResult(Result<(byte[], int, int, bool)>.Success((copy, _lastFrameW, _lastFrameH, _lastFrameIsMono)));
            }
        }

        /// <summary>
        /// 短暂 pin 住 byte[]，拿到 IntPtr 用于 BMP 写入。
        /// </summary>
        private sealed class PinnedArray : IDisposable
        {
            private readonly GCHandle _handle;
            public IntPtr Ptr => _handle.AddrOfPinnedObject();
            public PinnedArray(byte[] data) { _handle = GCHandle.Alloc(data, GCHandleType.Pinned); }
            public void Dispose() { _handle.Free(); }
        }

        /// <summary>
        /// 保存 8-bit 灰度 BMP（带调色板）。传入的灰度数据每像素 1 字节。
        /// </summary>
        private static void SaveAs8BitBmp(IntPtr grayData, uint width, uint height, string filePath)
        {
            uint rowSize = ((width + 3) / 4) * 4;
            uint pixelDataSize = rowSize * height;
            uint paletteSize = 256 * 4;
            uint fileSize = 54 + paletteSize + pixelDataSize;

            using var fs = new FileStream(filePath, FileMode.Create);
            using var bw = new BinaryWriter(fs);

            // BITMAPFILEHEADER
            bw.Write((ushort)0x4D42);
            bw.Write(fileSize);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write(54u + paletteSize);

            // BITMAPINFOHEADER
            bw.Write(40u);
            bw.Write((int)width);
            bw.Write((int)height);
            bw.Write((ushort)1);
            bw.Write((ushort)8);
            bw.Write(0u);
            bw.Write(pixelDataSize);
            bw.Write(0);
            bw.Write(0);
            bw.Write(256u);
            bw.Write(256u);

            // Grayscale palette
            for (int i = 0; i < 256; i++)
                bw.Write((uint)(i | (i << 8) | (i << 16)));

            // Pixel data (bottom-up, raw mono bytes)
            byte[] row = new byte[rowSize];
            for (int y = (int)height - 1; y >= 0; y--)
            {
                IntPtr src = grayData + (int)(y * width);
                Marshal.Copy(src, row, 0, (int)width);
                bw.Write(row);
            }
        }

        /// <summary>
        /// 保存 24-bit BGR BMP。
        /// </summary>
        private static void SaveAs24BitBmp(IntPtr bgrData, uint width, uint height, string filePath)
        {
            uint rowSize = ((width * 3 + 3) / 4) * 4;
            uint pixelDataSize = rowSize * height;
            uint fileSize = 54 + pixelDataSize;

            using var fs = new FileStream(filePath, FileMode.Create);
            using var bw = new BinaryWriter(fs);

            // BITMAPFILEHEADER
            bw.Write((ushort)0x4D42);
            bw.Write(fileSize);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write(54u);

            // BITMAPINFOHEADER
            bw.Write(40u);
            bw.Write((int)width);
            bw.Write((int)height);
            bw.Write((ushort)1);
            bw.Write((ushort)24);
            bw.Write(0u);
            bw.Write(pixelDataSize);
            bw.Write(0);
            bw.Write(0);
            bw.Write(0u);
            bw.Write(0u);

            // Pixel data (BGR, bottom-up)
            byte[] row = new byte[rowSize];
            for (int y = (int)height - 1; y >= 0; y--)
            {
                IntPtr src = bgrData + (int)(y * width * 3);
                Marshal.Copy(src, row, 0, (int)(width * 3));
                bw.Write(row);
            }
        }
        public async Task<Result> StopCameraAsync()
        {
            if (!IsConnected)
                return Result.Fail(ResultCode.Fail, "未链接设备");
            var ret = _camera.MV_CC_StopGrabbing_NET();
            if (ret != 0) 
                return Result.Fail(ResultCode.Fail, "捕获失败");
            return Result.Success();
        }

      


        public void Dispose()
        {
            _camera.MV_CC_StopGrabbing_NET();
            _camera.MV_CC_RegisterImageCallBack_NET(null, IntPtr.Zero);
            _camera.MV_CC_CloseDevice_NET();
            _camera.MV_CC_DestroyDevice_NET();
        }
    }
}
