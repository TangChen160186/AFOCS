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
        public JogLeftViewModel(IAxisStateService axisService, IShell shell)
            : base(axisService, shell, "左工位手柄") { }

        protected override void InitStationAxes()
        {
            // 相机轴
            AddBusAxis(AxisId.LeftCamUpX, CameraAxes);
            AddBusAxis(AxisId.LeftCamUpY, CameraAxes);
            AddBusAxis(AxisId.LeftCamUpZ, CameraAxes);
            AddBusAxis(AxisId.LeftCamSideY, CameraAxes);

            // 耦合θ轴
            AddBusAxis(AxisId.LeftCouplingLThetaX, ThetaAxes);
            AddBusAxis(AxisId.LeftCouplingLThetaY, ThetaAxes);
            AddBusAxis(AxisId.LeftCouplingLThetaZ, ThetaAxes);
            AddBusAxis(AxisId.LeftCouplingRThetaX, ThetaAxes);
            AddBusAxis(AxisId.LeftCouplingRThetaY, ThetaAxes);
            AddBusAxis(AxisId.LeftCouplingRThetaZ, ThetaAxes);

            // 直线轴（暂未实现）
            AddLinearAxis(LinearAxisId.LeftCouplingLX);
            AddLinearAxis(LinearAxisId.LeftCouplingLY);
            AddLinearAxis(LinearAxisId.LeftCouplingLZ);
            AddLinearAxis(LinearAxisId.LeftCouplingRX);
            AddLinearAxis(LinearAxisId.LeftCouplingRY);
            AddLinearAxis(LinearAxisId.LeftCouplingRZ);

            // 夹爪（暂未实现）
            AddGripper(GripperId.LeftCouplingLGripper);
            AddGripper(GripperId.LeftCouplingRGripper);
        }
    }

    public interface IJogLeft : IJogStation { }
}
