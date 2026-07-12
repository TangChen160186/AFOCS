using AFOCS.App.Core;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;
using MvCamCtrl.NET;
using MvCameraControl;
using System.Runtime.InteropServices;

namespace AFOCS.App.Devices.Implementation
{
    public class HkCameraConfig
    {
        public string ChSerialNumber { get; set; } = "ChSerialNumber";
    }
    public class HkCamera<T>: IDevice where T:HkCameraConfig,new()
    {
        private readonly IConfigService _configService;
        private readonly ILogger<HkCamera<T>> _logger;
        public bool IsConnected { get; }
        public void Dispose()
        {
            // TODO release managed resources here
        }

        public HkCamera(IConfigService configService,ILogger<HkCamera<T>> logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            try
            {
              
                
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            throw new NotImplementedException();
        }

        public Result FindDevices(string name)
        {
            MyCamera.MV_CC_DEVICE_INFO_LIST deviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();
            var result = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE, ref deviceList);
            if (result != 0)
            {
                _logger.LogError($"CameraListAcq Error,result:{result}");
                return Result.Fail(ResultCode.Fail, $"CameraListAcq Error,result:{result}");
            }
            // 集合表达式：更简洁地表示空判断
            if (deviceList.nDeviceNum == 0)
                return Result.Fail(ResultCode.Fail, "Find Device Error");

            bool hasCam = false;

            // 使用 foreach 配合 Range 语法简化循环

            
            for (int i=0;i< deviceList.nDeviceNum;++i)
            {
                // 使用 MemoryMarshal.Cast 将 IntPtr 指向的内存直接解释为结构体
                var device = Marshal.PtrToStructure<MyCamera.MV_CC_DEVICE_INFO>(deviceList.pDeviceInfo[i]);

                if (device.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                {
                    // 同样使用 MemoryMarshal 读取 GigE 信息
                    var gigeInfo = Marshal.PtrToStructure<MyCamera.MV_GIGE_DEVICE_INFO>(
                        device.SpecialInfo.stGigEInfo.AsPointer() // 需要 unsafe
                    );

                    // 使用新的 IsNullOrEmpty 方法检查字符串（.NET 10 新增）
                    if (!string.IsNullOrEmpty(gigeInfo.chSerialNumber) &&
                        gigeInfo.chSerialNumber == name)
                    {
                        hasCam = true;
                        deviceInfo = device;
                        break; // 找到后立即跳出，提高性能
                    }
                }
            }

            return hasCam
                ? DeviceResult.Success
                : DeviceResult.Failure(AfocsErrorCode.CameraInitFailed, new Exception("Find Device Error"));
        }
        public Task<Result> StopAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result> ReConnectAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
    }
}
