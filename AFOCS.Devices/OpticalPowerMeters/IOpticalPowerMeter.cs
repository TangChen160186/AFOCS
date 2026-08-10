using AFOCS.Infrastructure;

namespace AFOCS.Devices.OpticalPowerMeters
{
    public interface IOpticalPowerMeter: IDevice
    {
        OpticalPowerMeterConfig GetConfig();
        Task SaveConfigAsync(OpticalPowerMeterConfig config);

        #region 光源相关

        Task<Result<bool>> GetOsReadyAsync(int slot);


        Task<Result<bool>> GetOsStatusAsync(int slot, int channel);

        // power:mw
        Task<Result> SetOsPowerAsync(int slot,int channel,double power);

        Task<Result<double>> GetOsPowerAsync(int slot, int channel);
        Task<Result<double[]>> GetOsPowerAsync(int slot);

        #endregion


        #region 功率计相关

        Task<Result<bool>> GetOpmReadyAsync(int slot);

        // dbm
        Task<Result<double>> GetOpmPowerAsync(int slot, int channel);
        Task<Result<double[]>> GetOpmPowerAsync(int slot);

        //dbm
        Task<Result> SetOpmOffsetAsync(int slot,int channel,double offset);

        Task<Result<double>> GetOpmOffsetAsync(int slot, int channel);



        #endregion
    }
}
