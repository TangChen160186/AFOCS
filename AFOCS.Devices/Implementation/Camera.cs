using System.Runtime.InteropServices;
using AFOCS.Infrastructure;
using MvCamCtrl.NET;
using Serilog;
using static MvCamCtrl.NET.MyCamera;

namespace AFOCS.Devices.Implementation
{
    public class HkCameraConfig
    {
        public string ChSerialNumber { get; set; } = "ChSerialNumber";
    }

    public class Camera<T>(IConfigService configService, ILogger logger) : ICamera
        where T : HkCameraConfig, new()
    {
        public uint Height { get; private set; }
        public uint Width { get; private set; }
        public uint WidthStep { get; private set; }
        public uint HeightStep { get; private set; }

        private readonly MyCamera _camera = new();
        private cbOutputExdelegate _outputCallback;

        public bool IsConnected => _camera.MV_CC_IsDeviceConnected_NET();



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

                var deviceInfo = FindCameraByChSerialNumber(config.ChSerialNumber);
                if (deviceInfo == null)
                    return Result.Fail(ResultCode.Fail, $"Find Device Error: Target camera 【{config.ChSerialNumber}】 not found");

                var success = OpenDevice(deviceInfo.Value);
                if(!success) 
                    return Result.Fail(ResultCode.Fail, $"Open Device fail");

                InitImageSize();
                InitCameraParm();
                SetImageCallback();
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
                uint width = pFrameInfo.nWidth;
                uint height = pFrameInfo.nHeight;

                ImageReceived?.Invoke(this, new ImagePreviewedEventArgs(pData, (int)width, (int)height));

            }
            catch (Exception ex)
            {
                logger.Error($"{ex}");
            }
        }
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
