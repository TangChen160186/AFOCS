using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using AFOCS.Infrastructure;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels
{
    // ============================================================
    // 单轴手柄条目
    // ============================================================

    public class JogAxisItem : INotifyPropertyChanged
    {
        private double _position;
        private double _speed;
        private bool _isMoving;
        private readonly IJogStation _owner;

        public AxisKind Kind { get; }
        public int AxisId { get; }
        public string Name { get; set; }

        public double Position
        {
            get => _position;
            set { _position = value; OnPropertyChanged(); }
        }
        public double Speed
        {
            get => _speed;
            set { _speed = value; OnPropertyChanged(); }
        }
        public bool IsMoving
        {
            get => _isMoving;
            set { _isMoving = value; OnPropertyChanged(); }
        }

        public ICommand JogPositiveCommand { get; }
        public ICommand JogNegativeCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand HomeCommand { get; }

        public JogAxisItem(IJogStation owner, AxisKind kind, int axisId, string name)
        {
            _owner = owner;
            Kind = kind;
            AxisId = axisId;
            Name = name;
            JogPositiveCommand = new AFOCS.Framework.Framework.RelayCommand(p => _ = JogPositiveAsync());
            JogNegativeCommand = new AFOCS.Framework.Framework.RelayCommand(p => _ = JogNegativeAsync());
            StopCommand = new AFOCS.Framework.Framework.RelayCommand(p => _ = StopAsync());
            HomeCommand = new AFOCS.Framework.Framework.RelayCommand(p => _ = HomeAsync());
        }

        private async Task JogPositiveAsync() => await _owner.MoveAxisAsync(this, +1);
        private async Task JogNegativeAsync() => await _owner.MoveAxisAsync(this, -1);
        private async Task StopAsync() => await _owner.StopAxisAsync(this);
        private async Task HomeAsync() => await _owner.HomeAxisAsync(this);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ============================================================
    // 手柄工位接口
    // ============================================================

    public interface IJogStation : ITool
    {
        double JogDistance { get; }
        double JogSpeed { get; }
        Task MoveAxisAsync(JogAxisItem item, int direction);
        Task StopAxisAsync(JogAxisItem item);
        Task HomeAxisAsync(JogAxisItem item);
    }

    // ============================================================
    // 手柄工位基类
    // ============================================================

    public abstract class JogStationViewModel : Tool, IJogStation
    {
        protected readonly IBusAxisDevice BusAxisDevice;
        private readonly IShell _shell;

        public override PaneLocation PreferredLocation => PaneLocation.Bottom;
        public override double PreferredWidth => 620;
        public override double PreferredHeight => 500;

        private double _jogDistance = 1;
        public double JogDistance
        {
            get => _jogDistance;
            set { _jogDistance = value; NotifyOfPropertyChange(); }
        }

        private double _jogSpeed = 10;
        public double JogSpeed
        {
            get => _jogSpeed;
            set { _jogSpeed = value; NotifyOfPropertyChange(); }
        }

        private string? _lastError;
        public string? LastError
        {
            get => _lastError;
            set { _lastError = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasError)); }
        }
        public bool HasError => !string.IsNullOrEmpty(LastError);

        public ObservableCollection<JogAxisItem> CameraAxes { get; } = [];
        public ObservableCollection<JogAxisItem> ThetaAxes { get; } = [];
        public ObservableCollection<JogAxisItem> LinearAxes { get; } = [];
        public ObservableCollection<JogAxisItem> Grippers { get; } = [];

        protected JogStationViewModel(IBusAxisDevice busAxisDevice, IShell shell, string displayName)
        {
            DisplayName = displayName;
            BusAxisDevice = busAxisDevice;
            _shell = shell;

            InitStationAxes();
            BusAxisDevice.AxisStateChanged += OnAxisStateChanged;
        }

        protected abstract void InitStationAxes();

        // ---- 错误提示 ----

        public void DismissError() => LastError = null;

        private async Task SafeRunAsync(string action, Func<Task> fn)
        {
            try { await fn(); }
            catch (Exception ex)
            {
                LastError = $"[{action}] {ex.Message}";
            }
        }

        // ---- 事件处理 ----

        private void OnAxisStateChanged(object? sender, AxisStateChangedEventArgs e)
        {
            var item = FindItem(e.Kind, e.AxisId);
            if (item == null) return;

            Execute.OnUIThread(() =>
            {
                item.Position = e.Position;
                item.Speed = e.Speed;
                item.IsMoving = e.IsMoving;
            });
        }

        private JogAxisItem? FindItem(AxisKind kind, int axisId) => kind switch
        {
            AxisKind.BusAxis => CameraAxes.Concat(ThetaAxes).FirstOrDefault(x => x.AxisId == axisId),
            AxisKind.LinearAxis => LinearAxes.FirstOrDefault(x => x.AxisId == axisId),
            AxisKind.Gripper => Grippers.FirstOrDefault(x => x.AxisId == axisId),
            _ => null
        };

        // ---- 运动命令（由 JogAxisItem 回调） ----

        public async Task MoveAxisAsync(JogAxisItem item, int direction)
        {
            await SafeRunAsync($"{item.Name} 点动", async () =>
            {
                var dist = Math.Abs(JogDistance) * direction;
                if (item.Kind == AxisKind.BusAxis)
                {
                    var axisId = (AxisId)item.AxisId;
                    await BusAxisDevice.MovePmoveAsync(
                        axisId: axisId,
                        distance: dist,
                        overrideMaxVel: JogSpeed > 0 ? JogSpeed : null);
                }
                else if (item.Kind == AxisKind.LinearAxis)
                {
                    LastError = "直线轴运动暂未实现";
                }
            });
        }

        public async Task StopAxisAsync(JogAxisItem item)
        {
            await SafeRunAsync($"{item.Name} 停止", async () =>
            {
                if (item.Kind == AxisKind.BusAxis)
                    await BusAxisDevice.StopAxisAsync((AxisId)item.AxisId);
            });
        }

        public async Task HomeAxisAsync(JogAxisItem item)
        {
            await SafeRunAsync($"{item.Name} 回零", async () =>
            {
                if (item.Kind == AxisKind.BusAxis)
                {
                    var axisId = (AxisId)item.AxisId;
                    await BusAxisDevice.MoveHomeAsync(axisId: axisId);
                }
            });
        }

        // ---- 生命周期 ----

        protected override async Task OnInitializedAsync(CancellationToken cancellationToken)
        {
            await base.OnInitializedAsync(cancellationToken);
            _shell.RegisterTool(this);
            if (!BusAxisDevice.IsAxisMonitoring)
                await BusAxisDevice.StartAxisMonitorAsync();
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            if (close)
                BusAxisDevice.StopAxisMonitor();
            return base.OnDeactivateAsync(close, cancellationToken);
        }

        // ---- 工具方法 ----

        protected void AddBusAxis(AxisId id, ObservableCollection<JogAxisItem> group, string? shortName = null)
        {
            group.Add(new JogAxisItem(this, AxisKind.BusAxis, (int)id, shortName ?? BusAxisDevice.GetAxisShortName(id)));
        }

        protected void AddLinearAxis(LinearAxisId id, string? shortName = null)
        {
            LinearAxes.Add(new JogAxisItem(this, AxisKind.LinearAxis, (int)id, shortName ?? GetLinearShortName(id)));
        }

        protected void AddGripper(GripperId id, string? shortName = null)
        {
            Grippers.Add(new JogAxisItem(this, AxisKind.Gripper, (int)id, shortName ?? GetGripperShortName(id)));
        }

        // ---- 短名称（界面用） ----

        protected static string GetLinearShortName(LinearAxisId id) => id switch
        {
            LinearAxisId.LeftCouplingLX => "耦合LX",
            LinearAxisId.LeftCouplingLY => "耦合LY",
            LinearAxisId.LeftCouplingLZ => "耦合LZ",
            LinearAxisId.LeftCouplingRX => "耦合RX",
            LinearAxisId.LeftCouplingRY => "耦合RY",
            LinearAxisId.LeftCouplingRZ => "耦合RZ",
            LinearAxisId.RightCouplingLX => "耦合LX",
            LinearAxisId.RightCouplingLY => "耦合LY",
            LinearAxisId.RightCouplingLZ => "耦合LZ",
            LinearAxisId.RightCouplingRX => "耦合RX",
            LinearAxisId.RightCouplingRY => "耦合RY",
            LinearAxisId.RightCouplingRZ => "耦合RZ",
            _ => id.ToString(),
        };

        protected static string GetGripperShortName(GripperId id) => id switch
        {
            GripperId.LeftCouplingLGripper => "左夹爪",
            GripperId.LeftCouplingRGripper => "右夹爪",
            GripperId.RightCouplingLGripper => "左夹爪",
            GripperId.RightCouplingRGripper => "右夹爪",
            _ => id.ToString(),
        };
    }
}
