using System.ComponentModel.Composition;
using AFOCS.Devices.Camera;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using AFOCS.Infrastructure.Extensions;
using Caliburn.Micro;
using Serilog;

namespace AFOCS.App.ViewModels;

public interface ICameraViewerTool : ITool;

/// <summary>
/// 相机查看工具：通过下拉列表在多个相机之间切换实时显示，
/// 右键窗口可保存当前帧为 PNG（弹出保存对话框由用户选择目录）。
/// </summary>
[Export]
[Export(typeof(ICameraViewerTool))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class CameraViewerViewModel(
    [ImportMany] IEnumerable<ICamera> cameras,
    ILogger logger,
    IEventAggregator events)
    : CameraToolViewModelBase(
        cameras.First(), cameras.First().GetType().GetDescription(), logger, events), ICameraViewerTool
{
    private readonly ICamera[] _cameraList = cameras.ToArray();

    /// <summary>可选相机名称列表（Description）</summary>
    public IReadOnlyList<string> CameraNames { get; }
        = cameras.Select(c => c.GetType().GetDescription()).ToArray();

    private string _selectedCamera = cameras.First().GetType().GetDescription();
    /// <summary>当前选中的相机名称</summary>
    public string SelectedCamera
    {
        get => _selectedCamera;
        set
        {
            if (Set(ref _selectedCamera, value))
            {
                var camera = _cameraList.FirstOrDefault(c => c.GetType().GetDescription() == value);
                if (camera != null)
                    SwitchCamera(camera, value);
            }
        }
    }

    public override PaneLocation PreferredLocation => PaneLocation.Right;
    public override double PreferredWidth => 640;
    public override double PreferredHeight => 540;
}
