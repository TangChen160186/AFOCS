using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Windows;
using AFOCS.App.Services;
using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Framework.Modules.Settings;
using Caliburn.Micro;

namespace AFOCS.App.ViewModels
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class MotionCardSettingsViewModel : Screen, ISettingsEditor
    {
        private readonly IMotionControlCard _card;
        private readonly IToastService _toastService;
        private readonly LeadShineMotionCardConfig _config = new();
        private bool _isModify;

        private readonly string[] _modifyProperties =
        [
            nameof(EniPath), nameof(IniPath), nameof(TimeoutMs),
        ];

        [ImportingConstructor]
        public MotionCardSettingsViewModel(IMotionControlCard card, IToastService toastService)
        {
            _card = card;
            _toastService = toastService;

            var config = card.GetConfig();
            _config.EniPath = config.EniPath;
            _config.IniPath = config.IniPath;
            _config.TimeoutMs = config.TimeoutMs;
        }

        public string SettingsPageName => "雷赛控制卡";
        public string SettingsPagePath => "设备配置";

        // ========== 生命周期 ==========

        protected override void OnViewAttached(object view, object context)
        {
            base.OnViewAttached(view, context);
            RefreshConnectionStatus();
            _ = RefreshBusStatusAsync();

            if (view is FrameworkElement fe)
                fe.Unloaded += OnViewUnloaded;
        }

        private void OnViewUnloaded(object? sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
                fe.Unloaded -= OnViewUnloaded;
        }

        // ========== 配置属性 ==========

        public string EniPath
        {
            get => _config.EniPath;
            set
            {
                if (_config.EniPath == value) return;
                _config.EniPath = value;
                NotifyOfPropertyChange();
            }
        }

        public string IniPath
        {
            get => _config.IniPath;
            set
            {
                if (_config.IniPath == value) return;
                _config.IniPath = value;
                NotifyOfPropertyChange();
            }
        }

        public int TimeoutMs
        {
            get => _config.TimeoutMs;
            set
            {
                if (_config.TimeoutMs == value) return;
                _config.TimeoutMs = value;
                NotifyOfPropertyChange();
            }
        }

        // ========== 连接状态 ==========

        public bool IsConnected => _card.IsConnected;

        public string StatusMessage
        {
            get;
            set => Set(ref field, value);
        } = string.Empty;

        public bool IsBusy
        {
            get;
            set => Set(ref field, value);
        }

        public void RefreshConnectionStatus()
        {
            NotifyOfPropertyChange(() => IsConnected);
            StatusMessage = IsConnected ? "EtherCAT 总线已连接" : "总线未连接";
        }

        // ========== 总线状态码 ==========

        private string _busErrorCode = "-";
        public string BusErrorCode
        {
            get => _busErrorCode;
            set
            {
                if (_busErrorCode == value) return;
                _busErrorCode = value;
                NotifyOfPropertyChange();
            }
        }

        private string _busStatusText = string.Empty;
        public string BusStatusText
        {
            get => _busStatusText;
            set
            {
                if (_busStatusText == value) return;
                _busStatusText = value;
                NotifyOfPropertyChange();
            }
        }

        public async Task RefreshBusStatusAsync()
        {
            try
            {
                var result = await _card.GetBusStatusAsync();
                if (result.IsSuccess)
                {
                    BusErrorCode = $"0x{result.Data.ErrorCode:X4}";
                    BusStatusText = result.Data.Description;
                }
                else
                {
                    BusErrorCode = "ERR";
                    BusStatusText = result.Message;
                }
            }
            catch
            {
                BusErrorCode = "ERR";
                BusStatusText = "读取总线状态异常";
            }
        }

        // ========== 操作 ==========

        public async Task SaveAsync()
        {


            IsBusy = true;
            StatusMessage = "正在保存...";
            try
            {
                await _card.SaveConfigAsync(_config);
                _isModify = false;
                StatusMessage = "配置已保存";
            }
            catch (Exception ex) { StatusMessage = $"保存异常: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        public async Task ReconnectAsync()
        {
            IsBusy = true;
            StatusMessage = "正在重连...";
            try
            {
                if (_isModify)
                {
                    await _card.SaveConfigAsync(_config);
                    _isModify = false;
                }
                var result = await _card.ReConnectAsync();
                StatusMessage = result.IsSuccess ? "重连成功" : $"重连失败: {result.Message}";
            }
            catch (Exception ex) { StatusMessage = $"重连异常: {ex.Message}"; }
            finally
            {
                IsBusy = false;
                NotifyOfPropertyChange(() => IsConnected);
                _ = RefreshBusStatusAsync();
            }
        }

        public async Task HotResetAsync()
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接"); return; }

            IsBusy = true;
            StatusMessage = "正在热复位...";
            try
            {
                var result = await _card.HotResetAsync();
                StatusMessage = result.IsSuccess ? "热复位成功（仅复位 EtherCAT 协议栈）" : $"热复位失败: {result.Message}";
            }
            catch (Exception ex) { StatusMessage = $"热复位异常: {ex.Message}"; }
            finally
            {
                IsBusy = false;
                _ = RefreshBusStatusAsync();
            }
        }

        public async Task ColdResetAsync()
        {
            if (!IsConnected) { _toastService.ShowWarning("设备未连接"); return; }

            IsBusy = true;
            StatusMessage = "正在冷复位（等待 15 秒）...";
            try
            {
                var result = await _card.ColdResetAsync();
                StatusMessage = result.IsSuccess ? "冷复位成功（板卡已重新初始化）" : $"冷复位失败: {result.Message}";
                NotifyOfPropertyChange(() => IsConnected);
            }
            catch (Exception ex) { StatusMessage = $"冷复位异常: {ex.Message}"; }
            finally
            {
                IsBusy = false;
                _ = RefreshBusStatusAsync();
            }
        }

        // ========== NotifyOfPropertyChange 重写 ==========

        public override void NotifyOfPropertyChange([CallerMemberName] string? propertyName = null)
        {
            base.NotifyOfPropertyChange(propertyName);

            if (_modifyProperties.Contains(propertyName))
            {
                _isModify = true;
            }
        }

        // ========== ISettingsEditor ==========

        public void ApplyChanges()
        {
            if (_isModify) _ = SaveAsync();
        }
    }
}
