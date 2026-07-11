using AFOCS.App.Core;

namespace AFOCS.App.Devices
{
    public interface IOpticalSwitch: IDevice
    {
        public Task<Result<bool>> SwitchChannelAsync(int group, int channel);

        public Task<Result<bool>> SwitchChannelAsync(int[] groups, int[] channels);

        public Task<Result<Dictionary<int,int>>> GetAllChannelStatusAsync();

        public Task<Result<string>> GetSnAsync();

        public Task<Result<string>> GetPnAsync();
    }
}
