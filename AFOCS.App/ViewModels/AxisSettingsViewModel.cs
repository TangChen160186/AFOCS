using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Windows;
using AFOCS.Devices;
using AFOCS.Framework.Modules.Settings;
using AFOCS.Infrastructure;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class AxisSettingsViewModel : Screen, ISettingsEditor
    {
        private readonly IMotionControlCard _motionCard;

        private AxisId _selectedAxis;
        private AxisConfig _currentConfig = new();
        private bool _isModify;

        private readonly string[] _modifyProperties =
        [
            nameof(Equiv), nameof(MinVel), nameof(MaxVel), nameof(Tacc), nameof(Tdec),
            nameof(StopVel), nameof(SPara),
            nameof(HomeMode), nameof(HomeLowVel), nameof(HomeHighVel),
            nameof(HomeTacc), nameof(HomeTdec), nameof(HomeOffsetPos),
            nameof(NegativeSoftLimit), nameof(PositiveSoftLimit), nameof(SoftLimitEnabled),
            nameof(MaxSpeed), nameof(PulsePerRev),
        ];

        private string _statusMessage = string.Empty;
        private bool _isBusy;

        [ImportingConstructor]
        public AxisSettingsViewModel(IMotionControlCard motionCard)
        {
            _motionCard = motionCard;

            AxisList = new ObservableCollection<AxisInfo>(
                Enum.GetValues<AxisId>().Select(a => new AxisInfo
                {
                    AxisId = a,
                    DisplayName = GetAxisDisplayName(a),
                    AxisNumber = (int)a
                }));

            SelectedAxisInfo = AxisList.FirstOrDefault();
            _ = InitializeAsync();
        }

        public string SettingsPageName => "总线轴配置";
        public string SettingsPagePath => "设备配置";

        // ========== 生命周期 ==========

        protected override void OnViewAttached(object view, object context)
        {
            base.OnViewAttached(view, context);
            if (view is FrameworkElement fe)
                fe.Unloaded += OnViewUnloaded;
        }

        private void OnViewUnloaded(object? sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
                fe.Unloaded -= OnViewUnloaded;
        }

        // ========== 轴列表 ==========

        public ObservableCollection<AxisInfo> AxisList { get; }

        private AxisInfo? _selectedAxisInfo;
        public AxisInfo? SelectedAxisInfo
        {
            get => _selectedAxisInfo;
            set
            {
                if (_selectedAxisInfo == value) return;
                _selectedAxisInfo = value;
                NotifyOfPropertyChange();
                if (value != null)
                {
                    _selectedAxis = value.AxisId;
                    LoadAxisConfig(value.AxisId);
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                NotifyOfPropertyChange();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                NotifyOfPropertyChange();
            }
        }

        public bool IsModify => _isModify;

        // ========== 运动测试 ==========

        private double _moveDistance = 10;
        public double MoveDistance
        {
            get => _moveDistance;
            set { _moveDistance = value; NotifyOfPropertyChange(); }
        }

        private bool _movePositive = true;
        public bool MovePositive
        {
            get => _movePositive;
            set { _movePositive = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(MoveDirectionText)); }
        }

        public string MoveDirectionText => _movePositive ? "正向 (+)" : "反向 (-)";

        private bool _isMoving;
        public bool IsMoving
        {
            get => _isMoving;
            set { _isMoving = value; NotifyOfPropertyChange(); }
        }

        // ========== 运动参数 ==========

        public double Equiv
        {
            get => _currentConfig.Motion.Equiv;
            set { _currentConfig.Motion.Equiv = value; NotifyOfPropertyChange(); }
        }
        public double MinVel
        {
            get => _currentConfig.Motion.MinVel;
            set { _currentConfig.Motion.MinVel = value; NotifyOfPropertyChange(); }
        }
        public double MaxVel
        {
            get => _currentConfig.Motion.MaxVel;
            set { _currentConfig.Motion.MaxVel = value; NotifyOfPropertyChange(); }
        }
        public double Tacc
        {
            get => _currentConfig.Motion.Tacc;
            set { _currentConfig.Motion.Tacc = value; NotifyOfPropertyChange(); }
        }
        public double Tdec
        {
            get => _currentConfig.Motion.Tdec;
            set { _currentConfig.Motion.Tdec = value; NotifyOfPropertyChange(); }
        }
        public double StopVel
        {
            get => _currentConfig.Motion.StopVel;
            set { _currentConfig.Motion.StopVel = value; NotifyOfPropertyChange(); }
        }
        public double SPara
        {
            get => _currentConfig.Motion.SPara;
            set { _currentConfig.Motion.SPara = value; NotifyOfPropertyChange(); }
        }

        // ========== 回零参数 ==========

        public ushort HomeMode
        {
            get => _currentConfig.Home.HomeMode;
            set { _currentConfig.Home.HomeMode = value; NotifyOfPropertyChange(); }
        }
        public double HomeLowVel
        {
            get => _currentConfig.Home.LowVel;
            set { _currentConfig.Home.LowVel = value; NotifyOfPropertyChange(); }
        }
        public double HomeHighVel
        {
            get => _currentConfig.Home.HighVel;
            set { _currentConfig.Home.HighVel = value; NotifyOfPropertyChange(); }
        }
        public double HomeTacc
        {
            get => _currentConfig.Home.Tacc;
            set { _currentConfig.Home.Tacc = value; NotifyOfPropertyChange(); }
        }
        public double HomeTdec
        {
            get => _currentConfig.Home.Tdec;
            set { _currentConfig.Home.Tdec = value; NotifyOfPropertyChange(); }
        }
        public double HomeOffsetPos
        {
            get => _currentConfig.Home.OffsetPos;
            set { _currentConfig.Home.OffsetPos = value; NotifyOfPropertyChange(); }
        }

        // ========== 软限位 ==========

        public double NegativeSoftLimit
        {
            get => _currentConfig.NegativeSoftLimit;
            set { _currentConfig.NegativeSoftLimit = value; NotifyOfPropertyChange(); }
        }
        public double PositiveSoftLimit
        {
            get => _currentConfig.PositiveSoftLimit;
            set { _currentConfig.PositiveSoftLimit = value; NotifyOfPropertyChange(); }
        }
        public bool SoftLimitEnabled
        {
            get => _currentConfig.SoftLimitEnabled;
            set { _currentConfig.SoftLimitEnabled = value; NotifyOfPropertyChange(); }
        }

        // ========== 其他 ==========

        public double MaxSpeed
        {
            get => _currentConfig.MaxSpeed;
            set { _currentConfig.MaxSpeed = value; NotifyOfPropertyChange(); }
        }
        public int PulsePerRev
        {
            get => _currentConfig.PulsePerRev;
            set { _currentConfig.PulsePerRev = value; NotifyOfPropertyChange(); }
        }

        // ========== 回零模式选项 ==========

        public ObservableCollection<HomeModeOption> HomeModeOptions { get; } = new()
        {
            new(33, "正向找Z相"),
            new(34, "负向找Z相"),
            new(1,  "找负限位反找Z相"),
            new(2,  "找正限位反找Z相"),
            new(17, "找负限位"),
            new(18, "找正限位"),
            new(35, "当前位置设为原点"),
        };

        // ========== 操作 ==========

        public async Task SaveCurrentAxisAsync()
        {
            IsBusy = true;
            StatusMessage = "保存中...";
            try
            {
                _motionCard.SetAxisConfig(_selectedAxis, _currentConfig);
                await _motionCard.SaveAllAxisConfigsAsync();
                _isModify = false;
                NotifyOfPropertyChange(nameof(IsModify));
                StatusMessage = "已保存";
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存失败: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ResetToDefault()
        {
            var defaults = _motionCard.GetDefaultAxisConfig(_selectedAxis);
            _currentConfig = defaults.Clone();
            _isModify = true;
            NotifyOfPropertyChange(nameof(IsModify));
            RefreshAllProperties();
            StatusMessage = "已重置为默认值";
        }

        public async Task MoveTestAsync()
        {
            if (_motionCard == null || !_motionCard.IsConnected)
            {
                StatusMessage = "运动控制卡未连接";
                return;
            }

            IsMoving = true;
            StatusMessage = "运动中...";
            try
            {
                var cfg = _currentConfig.Motion;
                var distance = _movePositive ? _moveDistance : -_moveDistance;
                var result = await _motionCard.MovePmoveAsync(
                    axis: (ushort)_selectedAxis,
                    distance: distance,
                    equiv: 8000000,
                    minVel: cfg.MinVel,
                    maxVel: cfg.MaxVel,
                    tacc: cfg.Tacc,
                    tdec: cfg.Tdec,
                    stopVel: cfg.StopVel,
                    sPara: cfg.SPara);

                if (result.IsSuccess)
                    StatusMessage = "移动完成";
                else
                    StatusMessage = $"移动失败: {result.Message}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"移动异常: {ex.Message}";
            }
            finally
            {
                IsMoving = false;
            }
        }

        public async Task StopAsync()
        {
            if (_motionCard == null || !_motionCard.IsConnected)
            {
                StatusMessage = "运动控制卡未连接";
                return;
            }

            StatusMessage = "停止中...";
            var result = await _motionCard.StopAxisAsync((ushort)_selectedAxis);
            StatusMessage = result.IsSuccess ? "已停止" : $"停止失败: {result.Message}";
            IsMoving = false;
        }

        // ========== NotifyOfPropertyChange 重写 ==========

        public override void NotifyOfPropertyChange([CallerMemberName] string? propertyName = null)
        {
            base.NotifyOfPropertyChange(propertyName);

            if (_modifyProperties.Contains(propertyName))
            {
                _isModify = true;
                NotifyOfPropertyChange(nameof(IsModify));
            }
        }

        // ========== ISettingsEditor ==========

        public void ApplyChanges()
        {
            if (_isModify) _ = SaveCurrentAxisAsync();
        }

        // ========== 内部方法 ==========

        private async Task InitializeAsync()
        {
            if (SelectedAxisInfo != null)
                LoadAxisConfig(SelectedAxisInfo.AxisId);
        }

        private void LoadAxisConfig(AxisId axisId)
        {
            var config = _motionCard.GetAxisConfig(axisId);
            _currentConfig = config.Clone();
            _isModify = false;
            NotifyOfPropertyChange(nameof(IsModify));
            RefreshAllProperties();
        }

        private void RefreshAllProperties()
        {
            NotifyOfPropertyChange(nameof(Equiv));
            NotifyOfPropertyChange(nameof(MinVel));
            NotifyOfPropertyChange(nameof(MaxVel));
            NotifyOfPropertyChange(nameof(Tacc));
            NotifyOfPropertyChange(nameof(Tdec));
            NotifyOfPropertyChange(nameof(StopVel));
            NotifyOfPropertyChange(nameof(SPara));
            NotifyOfPropertyChange(nameof(HomeMode));
            NotifyOfPropertyChange(nameof(HomeLowVel));
            NotifyOfPropertyChange(nameof(HomeHighVel));
            NotifyOfPropertyChange(nameof(HomeTacc));
            NotifyOfPropertyChange(nameof(HomeTdec));
            NotifyOfPropertyChange(nameof(HomeOffsetPos));
            NotifyOfPropertyChange(nameof(NegativeSoftLimit));
            NotifyOfPropertyChange(nameof(PositiveSoftLimit));
            NotifyOfPropertyChange(nameof(SoftLimitEnabled));
            NotifyOfPropertyChange(nameof(MaxSpeed));
            NotifyOfPropertyChange(nameof(PulsePerRev));
        }

        private static string GetAxisDisplayName(AxisId axisId)
        {
            return axisId switch
            {
                // 左工位
                AxisId.LeftCamUpX => "左工位-上相机模组X轴",
                AxisId.LeftCamUpY => "左工位-上相机模组Y轴",
                AxisId.LeftCamUpZ => "左工位-上相机模组Z轴",
                AxisId.LeftCamSideY => "左工位-侧相机Y轴",
                AxisId.LeftCouplingLThetaX => "左工位-左耦合θX轴",
                AxisId.LeftCouplingLThetaY => "左工位-左耦合θY轴",
                AxisId.LeftCouplingLThetaZ => "左工位-左耦合θZ轴",
                AxisId.LeftCouplingRThetaX => "左工位-右耦合θX轴",
                AxisId.LeftCouplingRThetaY => "左工位-右耦合θY轴",
                AxisId.LeftCouplingRThetaZ => "左工位-右耦合θZ轴",
                // 右工位
                AxisId.RightCamUpX => "右工位-上相机模组X轴",
                AxisId.RightCamUpY => "右工位-上相机模组Y轴",
                AxisId.RightCamUpZ => "右工位-上相机模组Z轴",
                AxisId.RightCamSideY => "右工位-侧相机Y轴",
                AxisId.RightCouplingLThetaX => "右工位-左耦合θX轴",
                AxisId.RightCouplingLThetaY => "右工位-左耦合θY轴",
                AxisId.RightCouplingLThetaZ => "右工位-左耦合θZ轴",
                AxisId.RightCouplingRThetaX => "右工位-右耦合θX轴",
                AxisId.RightCouplingRThetaY => "右工位-右耦合θY轴",
                AxisId.RightCouplingRThetaZ => "右工位-右耦合θZ轴",
                _ => axisId.ToString()
            };
        }
    }

    // ========== 辅助类型 ==========

    public class AxisInfo
    {
        public AxisId AxisId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int AxisNumber { get; set; }
        public string HeaderText => $"Axis{AxisNumber} — {DisplayName}";
    }

    public class HomeModeOption
    {
        public ushort Mode { get; }
        public string Label { get; }

        public HomeModeOption(ushort mode, string label)
        {
            Mode = mode;
            Label = $"{mode} — {label}";
        }
    }
}
