using AFOCS.App.Core;

namespace AFOCS.App.Devices
{
    public interface IProgrammablePowerSupply : IDevice
    {
        Task<Result> SetChannelStatusAsync(int channel, bool status);
        Task<Result<bool>> GetChannelStatusAsync(int channel);
        Task<Result> SetVoltageAndCurrentAsync(int channel, double voltage,double current);
        Task<Result<(double, double)>> GetVoltageAndCurrentAsync(int channel);
   
        Task<Result<string>> GetErrorStatusAsync();
    }
}