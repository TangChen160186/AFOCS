using AFOCS.Infrastructure;

namespace AFOCS.Devices.ProgrammablePowerSupply;

public interface IProgrammablePowerSupply : IDevice
{
    ProgrammablePowerSupplyConfig GetConfig();
    Task SaveConfigAsync(ProgrammablePowerSupplyConfig config);

    Task<Result> SetChannelStatusAsync(int channel, bool status);
    Task<Result<bool>> GetChannelStatusAsync(int channel);
    Task<Result> SetVoltageAndCurrentAsync(int channel, double voltage,double current);
    Task<Result<(double, double)>> GetVoltageAndCurrentAsync(int channel);
   
    Task<Result<string>> GetErrorStatusAsync();
}