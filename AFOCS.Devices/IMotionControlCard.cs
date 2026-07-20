using AFOCS.Infrastructure;

namespace AFOCS.Devices
{
    public interface IMotionControlCard : IDevice
    {
        Task<Result> HotResetAsync();

        /// <summary>读取单个输入位</summary>
        Task<Result<bool>> ReadInbitAsync(ushort bitNo);

        /// <summary>批量读取多个输入位（0~bitCount-1），返回位数组</summary>
        Task<Result<bool[]>> ReadInbitsAsync(ushort bitCount);

        /// <summary>读取单个输出位当前电平</summary>
        Task<Result<bool>> ReadOutbitAsync(ushort bitNo);

        /// <summary>设置单个输出位</summary>
        Task<Result> WriteOutbitAsync(ushort bitNo, bool on);

        /// <summary>读取轴当前位置（unit）</summary>
        Task<Result<double>> GetPositionAsync(ushort axis);

        /// <summary>读取轴当前速度（unit/s）</summary>
        Task<Result<double>> GetSpeedAsync(ushort axis);
    }
}
