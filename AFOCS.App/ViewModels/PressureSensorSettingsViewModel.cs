using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
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
    public abstract class PressureSensorSettingsViewModel(string name, IPressureSensor sensor) : Screen, ISettingsEditor
    {
        public string Name { get; } = name;

        string ISettingsEditor.SettingsPageName => Name;

        string ISettingsEditor.SettingsPagePath => "设备配置\\压力传感器";

        // ========== 配置编辑 ==========

        private readonly PressureSensorConfig _editConfig = sensor.GetConfig();

        private bool _isModify = false;

        private readonly string[] _modifyProperties =
        [
            nameof(SlaveAddress), nameof(MapX), nameof(MapY), nameof(MapZ), nameof(AlarmX), nameof(AlarmY),
            nameof(AlarmZ)
        ];
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

        public int ValueX
        {
            get;
            set => Set(ref field, value);
        }

        public int ValueY
        {
            get;
            set => Set(ref field,value);
        }

        public int ValueZ
        {
            get;
            set => Set(ref field, value);
        }

        // ========== 状态 ==========


        public bool IsConnected
        {
            get;
            set => Set(ref field, value);
        }
    


        public bool IsMonitoringDisplay
        {
            get;
            set => Set(ref field, value);
        }


        public string StatusMessage 
        {
            get;
            set => Set(ref field, value);
        } = "";


        public bool AlarmActiveX
        {
            get;
            set => Set(ref field, value);
        }

        public bool AlarmActiveY
        {
            get;
            set => Set(ref field, value);
        }

        public bool AlarmActiveZ
        {
            get;
            set => Set(ref field, value);
        }

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
            IsConnected = sensor.IsConnected;
            IsMonitoringDisplay = sensor.IsMonitoring;
            StatusMessage = sensor.IsConnected
                ? (sensor.IsMonitoring ? "已连接 · 监控中" : "已连接 · 未监控")
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
            sensor.DataChanged += OnDataChanged;
            sensor.AlarmTriggered += OnAlarmTriggered;
            ValueX = sensor.GetX();
            ValueY = sensor.GetY();
            ValueZ = sensor.GetZ();
        }

        private void Unsubscribe()
        {
            sensor.DataChanged -= OnDataChanged;
            sensor.AlarmTriggered -= OnAlarmTriggered;
        }

        // ========== 命令 ==========


        public async Task SaveAsync()
        {
            await sensor.SaveConfigAsync(_editConfig);
            _isModify = false;
            StatusMessage = "配置已保存";
            RefreshStatus();
        }

        public async Task ZeroXAsync()
        {
            StatusMessage = "X 通道清零中...";
            await sensor.ZeroXAsync();
            StatusMessage = "X 通道清零完成";
        }

        public async Task ZeroYAsync()
        {
            StatusMessage = "Y 通道清零中...";
            await sensor.ZeroYAsync();
            StatusMessage = "Y 通道清零完成";
        }

        public async Task ZeroZAsync()
        {
            StatusMessage = "Z 通道清零中...";
            await sensor.ZeroZAsync();
            StatusMessage = "Z 通道清零完成";
        }

        public async Task ZeroAllAsync()
        {
            StatusMessage = "全部通道清零中...";
            await sensor.ZeroAllAsync();
            StatusMessage = "全部通道清零完成";
        }

        // ========== ISettingsEditor ==========

        public override void NotifyOfPropertyChange([CallerMemberName]string? propertyName = null)
        {
            if (_modifyProperties.Contains(propertyName))
                _isModify = true;
            base.NotifyOfPropertyChange(propertyName);
        }

        public void ApplyChanges()
        {
            if(_isModify)
                _ = SaveAsync();
        }
    }

    // ====================================================================
    // 6 个子类 —— 每个子类对应一个物理传感器，MEF 导出为独立设置页
    // ====================================================================

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class PressureSensorLeftCouplingLSettingsViewModel(LeftCouplingLPressureSensor sensor)
        : PressureSensorSettingsViewModel("左耦合左压力传感器", sensor);

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class PressureSensorLeftCouplingRSettingsViewModel(LeftCouplingRPressureSensor sensor)
        : PressureSensorSettingsViewModel("左耦合右压力传感器", sensor);

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class PressureSensorLeftDispenseSettingsViewModel(LeftDispensePressureSensor sensor)
        : PressureSensorSettingsViewModel("左点胶压力传感器", sensor);

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class PressureSensorRightCouplingLSettingsViewModel(RightCouplingLPressureSensor sensor)
        : PressureSensorSettingsViewModel("右耦合左压力传感器", sensor);

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class PressureSensorRightCouplingRSettingsViewModel(RightCouplingRPressureSensor sensor)
        : PressureSensorSettingsViewModel("右耦合右压力传感器", sensor);

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class PressureSensorRightDispenseSettingsViewModel(RightDispensePressureSensor sensor)
        : PressureSensorSettingsViewModel("右点胶压力传感器", sensor);
}
