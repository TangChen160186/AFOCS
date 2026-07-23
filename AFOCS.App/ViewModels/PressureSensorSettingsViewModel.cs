using System.ComponentModel.Composition;
using System.Windows;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Modules.Settings;
using AFOCS.Infrastructure;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels
{
    /// <summary>
    /// 压力传感器设置基类 —— 每个子类对应一个物理传感器
    /// </summary>
    public abstract class PressureSensorSettingsViewModel : Screen, ISettingsEditor
    {
        private readonly IPressureSensor _sensor;

        protected PressureSensorSettingsViewModel(string name, IPressureSensor sensor)
        {
            Name = name;
            _sensor = sensor;
            _editConfig = sensor.GetConfig();
        }

        public string Name { get; }

        string ISettingsEditor.SettingsPageName => Name;

        string ISettingsEditor.SettingsPagePath => "设备配置\\压力传感器";

        // ========== 配置编辑 ==========

        private PressureSensorConfig _editConfig;

        public ushort SlaveAddress
        {
            get => _editConfig.SlaveAddress;
            set { _editConfig.SlaveAddress = value; NotifyOfPropertyChange(); }
        }

        public ushort MapX
        {
            get => _editConfig.GetSubIndex(PressureChannel.X);
            set { _editConfig.ChannelSubIndexMapping[PressureChannel.X] = value; NotifyOfPropertyChange(); }
        }

        public ushort MapY
        {
            get => _editConfig.GetSubIndex(PressureChannel.Y);
            set { _editConfig.ChannelSubIndexMapping[PressureChannel.Y] = value; NotifyOfPropertyChange(); }
        }

        public ushort MapZ
        {
            get => _editConfig.GetSubIndex(PressureChannel.Z);
            set { _editConfig.ChannelSubIndexMapping[PressureChannel.Z] = value; NotifyOfPropertyChange(); }
        }

        public int AlarmX
        {
            get => _editConfig.GetAlarmThreshold(PressureChannel.X);
            set { _editConfig.AlarmThresholds[PressureChannel.X] = value; NotifyOfPropertyChange(); }
        }

        public int AlarmY
        {
            get => _editConfig.GetAlarmThreshold(PressureChannel.Y);
            set { _editConfig.AlarmThresholds[PressureChannel.Y] = value; NotifyOfPropertyChange(); }
        }

        public int AlarmZ
        {
            get => _editConfig.GetAlarmThreshold(PressureChannel.Z);
            set { _editConfig.AlarmThresholds[PressureChannel.Z] = value; NotifyOfPropertyChange(); }
        }

        // ========== 实时值 ==========

        private int _valueX;
        public int ValueX { get => _valueX; set { _valueX = value; NotifyOfPropertyChange(); } }

        private int _valueY;
        public int ValueY { get => _valueY; set { _valueY = value; NotifyOfPropertyChange(); } }

        private int _valueZ;
        public int ValueZ { get => _valueZ; set { _valueZ = value; NotifyOfPropertyChange(); } }

        // ========== 状态 ==========

        private bool _isConnected;
        public bool IsConnected { get => _isConnected; set { _isConnected = value; NotifyOfPropertyChange(); } }

        private bool _isMonitoring;
        public bool IsMonitoringDisplay { get => _isMonitoring; set { _isMonitoring = value; NotifyOfPropertyChange(); } }

        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; NotifyOfPropertyChange(); } }

        private bool _alarmActiveX;
        public bool AlarmActiveX { get => _alarmActiveX; set { _alarmActiveX = value; NotifyOfPropertyChange(); } }

        private bool _alarmActiveY;
        public bool AlarmActiveY { get => _alarmActiveY; set { _alarmActiveY = value; NotifyOfPropertyChange(); } }

        private bool _alarmActiveZ;
        public bool AlarmActiveZ { get => _alarmActiveZ; set { _alarmActiveZ = value; NotifyOfPropertyChange(); } }

        // ========== 事件处理 ==========

        private void OnDataChanged(object? sender, PressureDataChangedEventArgs e)
        {
            ValueX = e.X;
            ValueY = e.Y;
            ValueZ = e.Z;
        }

        private void OnAlarmTriggered(object? sender, PressureAlarmEventArgs e)
        {
            switch (e.Channel)
            {
                case PressureChannel.X: AlarmActiveX = e.IsActive; break;
                case PressureChannel.Y: AlarmActiveY = e.IsActive; break;
                case PressureChannel.Z: AlarmActiveZ = e.IsActive; break;
            }
        }

        private void RefreshStatus()
        {
            IsConnected = _sensor.IsConnected;
            IsMonitoringDisplay = _sensor.IsMonitoring;
            StatusMessage = _sensor.IsConnected
                ? (_sensor.IsMonitoring ? "已连接 · 监控中" : "已连接 · 未监控")
                : "未连接";
        }

        // ========== 生命周期（每次切换 Tab 时视图被创建/销毁） ==========

        protected override void OnViewAttached(object view, object context)
        {
            base.OnViewAttached(view, context);

            Subscribe();
            RefreshStatus();

            if (view is FrameworkElement fe)
                fe.Unloaded += OnViewUnloaded;
        }

        private void OnViewUnloaded(object? sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
                fe.Unloaded -= OnViewUnloaded;
            Unsubscribe();
        }

        private void Subscribe()
        {
            _sensor.DataChanged += OnDataChanged;
            _sensor.AlarmTriggered += OnAlarmTriggered;
            ValueX = _sensor.GetX();
            ValueY = _sensor.GetY();
            ValueZ = _sensor.GetZ();
        }

        private void Unsubscribe()
        {
            _sensor.DataChanged -= OnDataChanged;
            _sensor.AlarmTriggered -= OnAlarmTriggered;
        }

        // ========== 命令 ==========

        public async Task RefreshValuesAsync()
        {
            var result = await _sensor.ReadAllAsync();
            if (result.IsSuccess)
            {
                ValueX = result.Data.X;
                ValueY = result.Data.Y;
                ValueZ = result.Data.Z;
            }
        }

        public async Task SaveAsync()
        {
            await _sensor.SaveConfigAsync(_editConfig);
            StatusMessage = "配置已保存";
            RefreshStatus();
        }

        public async Task ZeroXAsync()
        {
            StatusMessage = "X 通道清零中...";
            await _sensor.ZeroXAsync();
            StatusMessage = "X 通道清零完成";
        }

        public async Task ZeroYAsync()
        {
            StatusMessage = "Y 通道清零中...";
            await _sensor.ZeroYAsync();
            StatusMessage = "Y 通道清零完成";
        }

        public async Task ZeroZAsync()
        {
            StatusMessage = "Z 通道清零中...";
            await _sensor.ZeroZAsync();
            StatusMessage = "Z 通道清零完成";
        }

        public async Task ZeroAllAsync()
        {
            StatusMessage = "全部通道清零中...";
            await _sensor.ZeroAllAsync();
            StatusMessage = "全部通道清零完成";
        }

        // ========== ISettingsEditor ==========

        public void ApplyChanges()
        {
            _ = SaveAsync();
        }
    }

    // ====================================================================
    // 6 个子类 —— 每个子类对应一个物理传感器，MEF 导出为独立设置页
    // ====================================================================

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class PressureSensorLeftCouplingLSettingsViewModel : PressureSensorSettingsViewModel
    {
        [ImportingConstructor]
        public PressureSensorLeftCouplingLSettingsViewModel(LeftCouplingLPressureSensor sensor)
            : base("左耦合左压力传感器", sensor) { }
    }

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class PressureSensorLeftCouplingRSettingsViewModel : PressureSensorSettingsViewModel
    {
        [ImportingConstructor]
        public PressureSensorLeftCouplingRSettingsViewModel(LeftCouplingRPressureSensor sensor)
            : base("左耦合右压力传感器", sensor) { }
    }

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class PressureSensorLeftDispenseSettingsViewModel : PressureSensorSettingsViewModel
    {
        [ImportingConstructor]
        public PressureSensorLeftDispenseSettingsViewModel(LeftDispensePressureSensor sensor)
            : base("左点胶压力传感器", sensor) { }
    }

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class PressureSensorRightCouplingLSettingsViewModel : PressureSensorSettingsViewModel
    {
        [ImportingConstructor]
        public PressureSensorRightCouplingLSettingsViewModel(RightCouplingLPressureSensor sensor)
            : base("右耦合左压力传感器", sensor) { }
    }

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class PressureSensorRightCouplingRSettingsViewModel : PressureSensorSettingsViewModel
    {
        [ImportingConstructor]
        public PressureSensorRightCouplingRSettingsViewModel(RightCouplingRPressureSensor sensor)
            : base("右耦合右压力传感器", sensor) { }
    }

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class PressureSensorRightDispenseSettingsViewModel : PressureSensorSettingsViewModel
    {
        [ImportingConstructor]
        public PressureSensorRightDispenseSettingsViewModel(RightDispensePressureSensor sensor)
            : base("右点胶压力传感器", sensor) { }
    }
}
