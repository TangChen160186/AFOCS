using System.ComponentModel.Composition;
using AFOCS.Devices;
using AFOCS.Framework.Framework.Services;
using AFOCS.Infrastructure;

namespace AFOCS.App.ViewModels
{
    [Export(typeof(IJogLeft))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class JogLeftViewModel : JogStationViewModel, IJogLeft
    {
        [ImportingConstructor]
        public JogLeftViewModel(IBusAxisDevice busAxisDevice, IShell shell)
            : base(busAxisDevice, shell, "左工位手柄") { }

        protected override void InitStationAxes()
        {
            AddBusAxis(AxisId.LeftCamUpX, CameraAxes);
            AddBusAxis(AxisId.LeftCamUpY, CameraAxes);
            AddBusAxis(AxisId.LeftCamUpZ, CameraAxes);
            AddBusAxis(AxisId.LeftCamSideY, CameraAxes);

            AddBusAxis(AxisId.LeftCouplingLThetaX, ThetaAxes);
            AddBusAxis(AxisId.LeftCouplingLThetaY, ThetaAxes);
            AddBusAxis(AxisId.LeftCouplingLThetaZ, ThetaAxes);
            AddBusAxis(AxisId.LeftCouplingRThetaX, ThetaAxes);
            AddBusAxis(AxisId.LeftCouplingRThetaY, ThetaAxes);
            AddBusAxis(AxisId.LeftCouplingRThetaZ, ThetaAxes);

            AddLinearAxis(LinearAxisId.LeftCouplingLX);
            AddLinearAxis(LinearAxisId.LeftCouplingLY);
            AddLinearAxis(LinearAxisId.LeftCouplingLZ);
            AddLinearAxis(LinearAxisId.LeftCouplingRX);
            AddLinearAxis(LinearAxisId.LeftCouplingRY);
            AddLinearAxis(LinearAxisId.LeftCouplingRZ);

            AddGripper(0, "左夹爪");
            AddGripper(1, "右夹爪");
        }
    }

    public interface IJogLeft : IJogStation { }
}
