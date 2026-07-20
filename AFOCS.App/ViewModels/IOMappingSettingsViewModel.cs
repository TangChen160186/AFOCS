using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AFOCS.Devices;
using AFOCS.Framework.Modules.Settings;
using AFOCS.Infrastructure;
using Gemini.Modules.Settings;

namespace AFOCS.App.ViewModels
{
    public class BoolToBrushConverter : IValueConverter
    {
        public Brush? OnBrush { get; set; }
        public Brush? OffBrush { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? OnBrush ?? Brushes.Green : OffBrush ?? Brushes.Gray;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class IOEditItem : INotifyPropertyChanged
    {
        public string Name { get; }
        public string SignalName { get; }

        private int _bitNo;
        public int BitNo
        {
            get => _bitNo;
            set { _bitNo = value; OnPropertyChanged(); }
        }

        public string Module { get; }

        private bool _isHigh;
        public bool IsHigh
        {
            get => _isHigh;
            set { _isHigh = value; OnPropertyChanged(); }
        }

        private DateTime _lastChange = DateTime.MinValue;
        public DateTime LastChange
        {
            get => _lastChange;
            set { _lastChange = value; OnPropertyChanged(); }
        }

        public bool IsOutput { get; }

        private bool _activeHigh;
        /// <summary>是否高电平有效（true=高有效，false=低有效）</summary>
        public bool ActiveHigh
        {
            get => _activeHigh;
            set { _activeHigh = value; OnPropertyChanged(); }
        }

        public IOEditItem(string name, string signalName, int bitNo, string module, bool isOutput = false, bool activeHigh = true)
        {
            Name = name;
            SignalName = signalName;
            _bitNo = bitNo;
            Module = module;
            IsOutput = isOutput;
            _activeHigh = activeHigh;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => execute();
    }

    public class RelayCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => canExecute?.Invoke((T?)parameter) ?? true;
        public void Execute(object? parameter) => execute((T?)parameter);
    }

    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class IOMappingSettingsViewModel : INotifyPropertyChanged, ISettingsEditor, ICancelableSettingsEditor, IDisposable
    {
        private readonly IIOService _io;

        public string SettingsPageName => "IO 配置";
        public string SettingsPagePath => "设备配置";

        public ObservableCollection<IOEditItem> InputItems { get; } = [];
        public ObservableCollection<IOEditItem> OutputItems { get; } = [];

        public ICommand SaveCommand { get; }
        public ICommand ResetDefaultCommand { get; }
        public ICommand ToggleOutputCommand { get; }

        private string _status = "未监控";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public void ApplyChanges()
        {
            foreach (var item in InputItems)
            {
                if (Enum.TryParse<AllInputs>(item.SignalName, out var signal))
                {
                    _io.SetInputBitNo(signal, item.BitNo);
                    _io.SetInputActiveHigh(signal, item.ActiveHigh);
                }
            }
            foreach (var item in OutputItems)
            {
                if (Enum.TryParse<AllOutputs>(item.SignalName, out var signal))
                {
                    _io.SetOutputBitNo(signal, item.BitNo);
                    _io.SetOutputActiveHigh(signal, item.ActiveHigh);
                }
            }
            _ = _io.SaveAsync();
        }

        public async void CancelChanges()
        {
            // 从磁盘重新加载配置，覆盖内存中已修改的值
            await _io.LoadAsync();
            // 刷新 UI 列表为配置中的值
            LoadItems();
        }

        [ImportingConstructor]
        public IOMappingSettingsViewModel(IIOService io)
        {
            _io = io;

            SaveCommand = new RelayCommand(ApplyChanges);
            ResetDefaultCommand = new RelayCommand(ResetToDefault);
            ToggleOutputCommand = new RelayCommand<string>(ToggleOutput);

            LoadItems();

            _io.InputChanged += OnInputChanged;
            Status = _io.IsMonitoring ? "监控中" : "已停止";
        }

        private void LoadItems()
        {
            InputItems.Clear();
            OutputItems.Clear();

            var config = _io.GetConfig();

            foreach (var kv in SignalNames.Module1)
                InputItems.Add(MakeInput(kv, config, "左工位-通用(M1)"));
            foreach (var kv in SignalNames.Module2)
                InputItems.Add(MakeInput(kv, config, "左工位-真空(M2)"));
            foreach (var kv in SignalNames.Module3)
                InputItems.Add(MakeInput(kv, config, "右工位-通用(M3)"));
            foreach (var kv in SignalNames.Module4)
                InputItems.Add(MakeInput(kv, config, "右工位-真空(M4)"));

            foreach (var kv in SignalNames.Module5)
                OutputItems.Add(MakeOutput(kv, config, "左工位-通用(M5)"));
            foreach (var kv in SignalNames.Module6)
                OutputItems.Add(MakeOutput(kv, config, "左工位-真空(M6)"));
            foreach (var kv in SignalNames.Module7)
                OutputItems.Add(MakeOutput(kv, config, "右工位-通用(M7)"));
            foreach (var kv in SignalNames.Module8)
                OutputItems.Add(MakeOutput(kv, config, "右工位-真空(M8)"));

            // 监听 ActiveHigh 变化，即时刷新状态指示
            foreach (var item in InputItems)
                item.PropertyChanged += OnInputItemPropertyChanged;
            foreach (var item in OutputItems)
                item.PropertyChanged += OnOutputItemPropertyChanged;

            // 读取当前输入状态
            foreach (var item in InputItems)
            {
                if (Enum.TryParse<AllInputs>(item.SignalName, out var signal))
                    item.IsHigh = _io.GetState(signal);
            }

            // 异步读取输出口当前状态
            _ = ReadOutputStatesAsync();
        }

        private async Task ReadOutputStatesAsync()
        {
            foreach (var item in OutputItems)
            {
                if (!Enum.TryParse<AllOutputs>(item.SignalName, out var signal)) continue;
                var logical = await _io.ReadOutputAsync(signal);
                if (logical.HasValue)
                    item.IsHigh = logical.Value;
            }
        }

        private static IOEditItem MakeInput(KeyValuePair<AllInputs, string> kv, IOMappingConfig config, string module)
        {
            var signalName = kv.Key.ToString();
            var bitNo = config.Inputs.TryGetValue(signalName, out var b) ? b : (int)kv.Key;
            var activeHigh = config.InputActives.TryGetValue(signalName, out var a) ? a : true;
            return new IOEditItem(kv.Value, signalName, bitNo, module, activeHigh: activeHigh);
        }

        private static IOEditItem MakeOutput(KeyValuePair<AllOutputs, string> kv, IOMappingConfig config, string module)
        {
            var signalName = kv.Key.ToString();
            var bitNo = config.Outputs.TryGetValue(signalName, out var b) ? b : (int)kv.Key;
            var activeHigh = config.OutputActives.TryGetValue(signalName, out var a) ? a : true;
            return new IOEditItem(kv.Value, signalName, bitNo, module, isOutput: true, activeHigh: activeHigh);
        }

        private void OnInputChanged(object? sender, IOStateChangedEventArgs e)
        {
            var item = InputItems.FirstOrDefault(x => x.SignalName == e.Signal.ToString());
            if (item == null) return;

            item.IsHigh = e.NewValue;
            item.LastChange = e.Timestamp;
            Status = _io.IsMonitoring ? "监控中" : "已停止";
        }

        private void OnInputItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IOEditItem.ActiveHigh)) return;
            if (sender is not IOEditItem item) return;
            if (!Enum.TryParse<AllInputs>(item.SignalName, out var signal)) return;

            _io.SetInputActiveHigh(signal, item.ActiveHigh);
            item.IsHigh = _io.GetState(signal);
        }

        private async void OnOutputItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IOEditItem.ActiveHigh)) return;
            if (sender is not IOEditItem item) return;
            if (!Enum.TryParse<AllOutputs>(item.SignalName, out var signal)) return;

            _io.SetOutputActiveHigh(signal, item.ActiveHigh);

            var logical = await _io.ReadOutputAsync(signal);
            if (logical.HasValue)
                item.IsHigh = logical.Value;
        }

        private async void ToggleOutput(string? signalName)
        {
            if (signalName == null) return;
            if (!Enum.TryParse<AllOutputs>(signalName, out var signal)) return;

            var item = OutputItems.FirstOrDefault(x => x.SignalName == signalName);
            var newState = item != null && !item.IsHigh;
            await _io.WriteOutputAsync(signal, newState);
            if (item != null) item.IsHigh = newState;
        }

        private void ResetToDefault()
        {
            // 临时解绑，防止 ActiveHigh 变更触发同步到服务
            foreach (var item in InputItems)
                item.PropertyChanged -= OnInputItemPropertyChanged;
            foreach (var item in OutputItems)
                item.PropertyChanged -= OnOutputItemPropertyChanged;

            foreach (var item in InputItems)
            {
                if (Enum.TryParse<AllInputs>(item.SignalName, out var _))
                {
                    item.BitNo = (int)Enum.Parse<AllInputs>(item.SignalName);
                    item.ActiveHigh = true;
                }
            }
            foreach (var item in OutputItems)
            {
                if (Enum.TryParse<AllOutputs>(item.SignalName, out var _))
                {
                    item.BitNo = (int)Enum.Parse<AllOutputs>(item.SignalName);
                    item.ActiveHigh = true;
                }
            }

            foreach (var item in InputItems)
                item.PropertyChanged += OnInputItemPropertyChanged;
            foreach (var item in OutputItems)
                item.PropertyChanged += OnOutputItemPropertyChanged;
        }

        public void Dispose()
        {
            _io.InputChanged -= OnInputChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
