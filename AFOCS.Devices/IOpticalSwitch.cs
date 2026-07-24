using AFOCS.Devices.Implementation;
using AFOCS.Infrastructure;

namespace AFOCS.Devices
{
    public interface IOpticalSwitch: IDevice
    {
        OpticalSwitchConfig GetConfig();
        Task SaveConfigAsync(OpticalSwitchConfig config);
        public Task<Result<bool>> SwitchChannelAsync(int group, int channel);

        public Task<Result<bool>> SwitchChannelAsync(int[] groups, int[] channels);

        public Task<Result<Dictionary<int,int>>> GetAllChannelStatusAsync();

        public Task<Result<string>> GetSnAsync();

        public Task<Result<string>> GetPnAsync();
    }
}
