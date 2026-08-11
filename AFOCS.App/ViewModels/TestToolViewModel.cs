using AFOCS.Devices.BusAxisDevice;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using System.ComponentModel.Composition;

namespace AFOCS.App.ViewModels;

public interface ITestTool : ITool;

[Export]
[Export(typeof(ITestTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class TestToolViewModel(
    IBusAxisDevice busAxisDevice, IFlowExecutionService flowExecutionService) : Tool, ITestTool
{
    private readonly IBusAxisDevice _busAxisDevice = busAxisDevice;

    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 300;
    public override double PreferredHeight => 200;

    public override string DisplayName => "功能测试";

    public async void Test()
    {
        await flowExecutionService.ExecuteFlowAsync(@"C:\Users\16018\OneDrive\Desktop\Untitled 1.nflow");
        await _busAxisDevice.MovePmoveAsync(BusAxisId.LeftCamUpX, -37672, 1);
    }

    
}
