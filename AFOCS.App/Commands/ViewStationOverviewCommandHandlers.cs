using System.ComponentModel.Composition;
using System.Windows;
using AFOCS.App.ViewModels;
using AFOCS.Framework.Framework.Commands;
using Caliburn.Micro;

namespace AFOCS.App.Commands;

[CommandHandler]
public class ViewLeftStationOverviewCommandHandler : CommandHandlerBase<ViewLeftStationOverviewCommandDefinition>
{
    private static Window? _window;
    private readonly IWindowManager _windowManager;

    [ImportingConstructor]
    public ViewLeftStationOverviewCommandHandler(IWindowManager windowManager)
    {
        _windowManager = windowManager;
    }

    public override async Task Run(Command command)
    {
        if (_window != null)
        {
            BringToFront(_window);
            return;
        }

        var viewModel = IoC.Get<LeftStationOverviewViewModel>();
        await _windowManager.ShowWindowAsync(viewModel);

        _window = viewModel.GetView() as Window;
        if (_window != null)
            _window.Closed += (_, _) => _window = null;
    }

    private static void BringToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }
}

[CommandHandler]
public class ViewRightStationOverviewCommandHandler : CommandHandlerBase<ViewRightStationOverviewCommandDefinition>
{
    private static Window? _window;
    private readonly IWindowManager _windowManager;

    [ImportingConstructor]
    public ViewRightStationOverviewCommandHandler(IWindowManager windowManager)
    {
        _windowManager = windowManager;
    }

    public override async Task Run(Command command)
    {
        if (_window != null)
        {
            BringToFront(_window);
            return;
        }

        var viewModel = IoC.Get<RightStationOverviewViewModel>();
        await _windowManager.ShowWindowAsync(viewModel);

        _window = viewModel.GetView() as Window;
        if (_window != null)
            _window.Closed += (_, _) => _window = null;
    }

    private static void BringToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }
}