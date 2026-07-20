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
        public JogRightViewModel(IAxisStateService axisService, IShell shell)
            : base(axisService, shell, "右工位手柄") { }

        protected override void InitStationAxes()
        {
            // 相机轴
            AddBusAxis(AxisId.RightCamUpX, CameraAxes);
            AddBusAxis(AxisId.RightCamUpY, CameraAxes);
            AddBusAxis(AxisId.RightCamUpZ, CameraAxes);
            AddBusAxis(AxisId.RightCamSideY, CameraAxes);

            // 耦合θ轴
            AddBusAxis(AxisId.RightCouplingLThetaX, ThetaAxes);
            AddBusAxis(AxisId.RightCouplingLThetaY, ThetaAxes);
            AddBusAxis(AxisId.RightCouplingLThetaZ, ThetaAxes);
            AddBusAxis(AxisId.RightCouplingRThetaX, ThetaAxes);
            AddBusAxis(AxisId.RightCouplingRThetaY, ThetaAxes);
            AddBusAxis(AxisId.RightCouplingRThetaZ, ThetaAxes);

            // 直线轴（暂未实现）
            AddLinearAxis(LinearAxisId.RightCouplingLX);
            AddLinearAxis(LinearAxisId.RightCouplingLY);
            AddLinearAxis(LinearAxisId.RightCouplingLZ);
            AddLinearAxis(LinearAxisId.RightCouplingRX);
            AddLinearAxis(LinearAxisId.RightCouplingRY);
            AddLinearAxis(LinearAxisId.RightCouplingRZ);

            // 夹爪（暂未实现）
            AddGripper(GripperId.RightCouplingLGripper);
            AddGripper(GripperId.RightCouplingRGripper);
        }
    }

    public interface IJogRight : IJogStation { }
}
