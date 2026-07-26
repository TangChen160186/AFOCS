using System.ComponentModel.Composition;
using AFOCS.Devices;
using AFOCS.Framework.Framework.Services;
using AFOCS.Infrastructure;

namespace AFOCS.App.ViewModels
{
    [Export(typeof(IJogRight))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class JogRightViewModel : JogStationViewModel, IJogRight
    {
        [ImportingConstructor]
        public JogRightViewModel(IBusAxisDevice busAxisDevice, IShell shell)
            : base(busAxisDevice, shell, "右工位手柄") { }

        protected override void InitStationAxes()
        {
            AddBusAxis(AxisId.RightCamUpX, CameraAxes);
            AddBusAxis(AxisId.RightCamUpY, CameraAxes);
            AddBusAxis(AxisId.RightCamUpZ, CameraAxes);
            AddBusAxis(AxisId.RightCamSideY, CameraAxes);

            AddBusAxis(AxisId.RightCouplingLThetaX, ThetaAxes);
            AddBusAxis(AxisId.RightCouplingLThetaY, ThetaAxes);
            AddBusAxis(AxisId.RightCouplingLThetaZ, ThetaAxes);
            AddBusAxis(AxisId.RightCouplingRThetaX, ThetaAxes);
            AddBusAxis(AxisId.RightCouplingRThetaY, ThetaAxes);
            AddBusAxis(AxisId.RightCouplingRThetaZ, ThetaAxes);

            AddLinearAxis(LinearAxisId.RightCouplingLX);
            AddLinearAxis(LinearAxisId.RightCouplingLY);
            AddLinearAxis(LinearAxisId.RightCouplingLZ);
            AddLinearAxis(LinearAxisId.RightCouplingRX);
            AddLinearAxis(LinearAxisId.RightCouplingRY);
            AddLinearAxis(LinearAxisId.RightCouplingRZ);

            AddGripper(0, "左夹爪");
            AddGripper(1, "右夹爪");
        }
    }

    public interface IJogRight : IJogStation { }
}
