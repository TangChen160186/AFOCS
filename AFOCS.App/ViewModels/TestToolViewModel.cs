using System.ComponentModel.Composition;
using AFOCS.App.Services;
using AFOCS.Devices;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;

namespace AFOCS.App.ViewModels;

public interface ITestTool : ITool { }

[Export]
[Export(typeof(ITestTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class TestToolViewModel(IToastService toastService,IBusAxisDevice _axisDevice) : Tool, ITestTool
{
    private readonly IToastService _toastService = toastService;

    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 400;
    public override double PreferredHeight => 500;

    public override string DisplayName => "功能测试";

    public async void Test()
    {
        await _axisDevice.MovePmoveAsync(AxisId.LeftCamUpX,-37672,1);
        Console.WriteLine();
    }
}
