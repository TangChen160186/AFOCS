using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Windows;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Modules.Settings;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels.Settings
{
    /// <summary>
    /// 夹爪设置基类 —— 每个子类对应一个物理夹爪
    /// </summary>
    public abstract class GripperSettingsViewModel(string name, ISmcGripper gripper) : Screen, ISettingsEditor
    {
        protected readonly ISmcGripper Gripper = gripper;

        public string Name { get; } = name;

        string ISettingsEditor.SettingsPageName => Name;

        string ISettingsEditor.SettingsPagePath => "设备配置\\雷赛板卡\\夹爪";

        // ========== 配置编辑 ==========

        private readonly SmcGripperConfig _editConfig = gripper.GetConfig();

        private bool _isModify;

        private readonly string[] _modifyProperties = [nameof(SlaveAddress)];

        public ushort SlaveAddress
        {
            get => _editConfig.SlaveAddress;
            set { _editConfig.SlaveAddress = value; NotifyOfPropertyChange(); }
        }

        // ========== 实时值 ==========

        public int CurrentPosition
        {
            get;
            set => Set(ref field, value);
        }

        public bool IsEnabled
        {
            get;
            set => Set(ref field, value);
        }

        public bool IsAlarm
        {
            get;
            set => Set(ref field, value);
        }

        public ushort StatusWord
        {
            get;
            set => Set(ref field, value);
        }

        // ========== 连接状态 ==========

        public bool AreConnected
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

        // ========== 事件处理 ==========

        private void OnDataChanged(object? sender, GripperDataChangedEventArgs e)
        {
            CurrentPosition = e.CurrentPosition;
            IsEnabled = e.IsEnabled;
            IsAlarm = e.IsAlarm;
            StatusWord = e.StatusWord;
        }

        private void RefreshStatus()
        {
            AreConnected = Gripper.IsConnected;
            IsMonitoringDisplay = Gripper.IsMonitoring;
            StatusMessage = Gripper.IsConnected
                ? (Gripper.IsMonitoring ? "已连接 · 监控中" : "已连接 · 未监控")
                : "未连接";
        }

        // ========== 生命周期 ==========

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
            Gripper.DataChanged += OnDataChanged;
        }

        private void Unsubscribe()
        {
            Gripper.DataChanged -= OnDataChanged;
        }

        // ========== 功能测试 ==========

        public ushort TestSpeed
        {
            get;
            set => Set(ref field, value);
        } = 50;

        public ushort OpenPosition
        {
            get;
            set => Set(ref field, value);
        } = 500;

        public ushort ClosePosition
        {
            get;
            set => Set(ref field, value);
        } = 0;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; NotifyOfPropertyChange(); }
        }

        public async Task OpenAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = "正在打开...";
            var result = await Gripper.MoveAsync(TestSpeed, OpenPosition);
            StatusMessage = result.IsSuccess ? "打开完成" : $"打开失败: {result.Message}";
            IsBusy = false;
            RefreshStatus();
        }

        public async Task CloseAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = "正在关闭...";
            var result = await Gripper.MoveAsync(TestSpeed, ClosePosition);
            StatusMessage = result.IsSuccess ? "关闭完成" : $"关闭失败: {result.Message}";
            IsBusy = false;
            RefreshStatus();
        }

        public async Task SaveAsync()
        {
            await Gripper.SaveConfigAsync(_editConfig);
            _isModify = false;
            StatusMessage = "配置已保存";
            RefreshStatus();
        }

        public async Task EnableAsync()
        {
            StatusMessage = "使能中...";
            var result = await Gripper.EnableAsync();
            StatusMessage = result.IsSuccess ? "使能完成" : $"使能失败: {result.Message}";
            RefreshStatus();
        }

        public async Task HomeAsync()
        {
            StatusMessage = "回零中...";
            var result = await Gripper.HomeAsync();
            StatusMessage = result.IsSuccess ? "回零完成" : $"回零失败: {result.Message}";
            RefreshStatus();
        }

        public async Task AlarmResetAsync()
        {
            StatusMessage = "报警复位中...";
            var result = await Gripper.AlarmResetAsync();
            StatusMessage = result.IsSuccess ? "报警复位完成" : $"报警复位失败: {result.Message}";
            RefreshStatus();
        }

        // ========== ISettingsEditor ==========

        public override void NotifyOfPropertyChange([CallerMemberName] string? propertyName = null)
        {
            if (_modifyProperties.Contains(propertyName))
                _isModify = true;
            base.NotifyOfPropertyChange(propertyName);
        }

        public void ApplyChanges()
        {
            if (_isModify)
                _ = SaveAsync();
        }
    }
    
    // ====================================================================
    // 4 个子类 —— 每个子类对应一个物理夹爪，MEF 导出为独立设置页
    // ====================================================================

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class GripperLeftCouplingLSettingsViewModel(LeftCouplingLGripper gripper)
        : GripperSettingsViewModel("左耦合左夹爪", gripper);

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class GripperLeftCouplingRSettingsViewModel(LeftCouplingRGripper gripper)
        : GripperSettingsViewModel("左耦合右夹爪", gripper);

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class GripperRightCouplingLSettingsViewModel(RightCouplingLGripper gripper)
        : GripperSettingsViewModel("右耦合左夹爪", gripper);

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [method: ImportingConstructor]
    public class GripperRightCouplingRSettingsViewModel(RightCouplingRGripper gripper)
        : GripperSettingsViewModel("右耦合右夹爪", gripper);
}
