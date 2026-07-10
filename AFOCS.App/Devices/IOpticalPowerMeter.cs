using AFOCS.App.Core;

namespace AFOCS.App.Devices
{

    public enum OSType
    {
        DFB,
        SLED,
        FP,
    }

    public interface IOpticalPowerMeter: IDevice
    {

        #region 光源相关

        Task<Result<bool>> GetOsReadyAsync(int slot);

        Task<Result<(OSType, int)>> GetOsInformationAsync(int slot);

        Task<Result> SetOsStatusAsync(int slot, int channel,bool status);

        Task<Result<bool>> GetOsStatusAsync(int slot, int channel);

        // power:mw
        Task<Result> SetOsPowerAsync(int slot,int channel,double power);

        Task<Result<double>> GetOsPowerAsync(int slot, int channel);
        Task<Result<List<double>>> GetOsPowerAsync(int slot);
        Task<Result<int>> GetOsWaveLengthAsync(int slot,int channel);

        #endregion


        #region 功率计相关

        Task<Result<bool>> GetOpmReadyAsync(int slot);

        Task<Result<int>> GetOpmWaveLengthAsync(int slot, int channel);
        Task<Result> SetOpmWaveLengthAsync(int slot, int channel, int waveLength);

        // dbm
        Task<Result<double>> GetOpmPowerAsync(int slot, int channel);
        Task<Result<List<double>>> GetOpmPowerAsync(int slot);

        //dbm
        Task<Result> SetOpmOffsetAsync(int slot,int channel,double offset);

        Task<Result<double>> GetOpmOffsetAsync(int slot, int channel);



        #endregion
    }
}
