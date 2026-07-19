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

        /// <summary>仅输入信号：当前高电平</summary>
        private bool _isHigh;
        public bool IsHigh
        {
            get => _isHigh;
            set { _isHigh = value; OnPropertyChanged(); }
        }

        /// <summary>最后变化时间</summary>
        private DateTime _lastChange = DateTime.MinValue;
        public DateTime LastChange
        {
            get => _lastChange;
            set { _lastChange = value; OnPropertyChanged(); }
        }

        public bool IsOutput { get; }

        public IOEditItem(string name, string signalName, int bitNo, string module, bool isOutput = false)
        {
            Name = name;
            SignalName = signalName;
            _bitNo = bitNo;
            Module = module;
            IsOutput = isOutput;
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
    public class IOMappingSettingsViewModel : INotifyPropertyChanged, ISettingsEditor, IDisposable
    {
        private readonly IIOMappingService _mapping;
        private readonly IIOMonitorService? _monitor;

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
                    _mapping.SetInputBitNo(signal, item.BitNo);
            }
            foreach (var item in OutputItems)
            {
                if (Enum.TryParse<AllOutputs>(item.SignalName, out var signal))
                    _mapping.SetOutputBitNo(signal, item.BitNo);
            }
            _ = _mapping.SaveAsync();
        }

        [ImportingConstructor]
        public IOMappingSettingsViewModel(IIOMappingService mapping,
            [Import(AllowDefault = true)] IIOMonitorService? monitor = null)
        {
            _mapping = mapping;
            _monitor = monitor;

            SaveCommand = new RelayCommand(ApplyChanges);
            ResetDefaultCommand = new RelayCommand(ResetToDefault);
            ToggleOutputCommand = new RelayCommand<string>(ToggleOutput);

            LoadItems();

            // 订阅 IO 状态变化
            if (_monitor != null)
            {
                _monitor.InputChanged += OnInputChanged;
                Status = _monitor.IsRunning ? "监控中" : "已停止";
            }
        }

        private void LoadItems()
        {
            InputItems.Clear();
            OutputItems.Clear();

            var config = _mapping.GetConfig();

            foreach (var kv in SignalNames.Module1)
                InputItems.Add(MakeInput(kv, config, "左工位-通用(M1)"));
            foreach (var kv in SignalNames.Module2)
                InputItems.Add(MakeInput(kv, config, "左工位-真空(M2)"));
            foreach (var kv in SignalNames.Module3)
                InputItems.Add(MakeInput(kv, config, "右工位-通用(M3)"));
            foreach (var kv in SignalNames.Module4)
                InputItems.Add(MakeInput(kv, config, "右工位-真空(M4)"));

            foreach (AllOutputs signal in Enum.GetValues<AllOutputs>())
            {
                var signalName = signal.ToString();
                var bitNo = config.Outputs.TryGetValue(signalName, out var b) ? b : (int)signal;
                var module = GetOutputModule(signalName);
                OutputItems.Add(new IOEditItem(signalName, signalName, bitNo, module, isOutput: true));
            }

            // 从监控服务读取当前输入状态
            if (_monitor != null)
            {
                foreach (var item in InputItems)
                {
                    if (Enum.TryParse<AllInputs>(item.SignalName, out var signal))
                        item.IsHigh = _monitor.GetState(signal);
                }
            }
        }

        private static IOEditItem MakeInput(KeyValuePair<AllInputs, string> kv, IOMappingConfig config, string module)
        {
            var signalName = kv.Key.ToString();
            var bitNo = config.Inputs.TryGetValue(signalName, out var b) ? b : (int)kv.Key;
            return new IOEditItem(kv.Value, signalName, bitNo, module);
        }

        private static string GetOutputModule(string signalName)
        {
            if (signalName.StartsWith("Right_"))
                return signalName.Contains("Vacuum") || signalName.Contains("UVLight") ||
                       signalName.Contains("FixtureVacuum") || signalName.Contains("GripperUV") ||
                       signalName.Contains("Heat") || signalName.Contains("Controller")
                    ? "右工位-真空(M8)" : "右工位-通用(M7)";
            return signalName.Contains("Vacuum") || signalName.Contains("UVLight") ||
                   signalName.Contains("GripperUV") || signalName.Contains("Heat") ||
                   signalName.Contains("Controller")
                ? "左工位-真空(M6)" : "左工位-通用(M5)";
        }

        private void OnInputChanged(object? sender, IOStateChangedEventArgs e)
        {
            var item = InputItems.FirstOrDefault(x => x.SignalName == e.Signal.ToString());
            if (item == null) return;

            item.IsHigh = e.NewValue;
            item.LastChange = e.Timestamp;

            if (_monitor != null)
                Status = _monitor.IsRunning ? "监控中" : "已停止";
        }

        private async void ToggleOutput(string? signalName)
        {
            if (signalName == null) return;
            if (!Enum.TryParse<AllOutputs>(signalName, out var signal)) return;

            // 查找当前项切换显示状态
            var item = OutputItems.FirstOrDefault(x => x.SignalName == signalName);
            var newState = item != null && !item.IsHigh;
            await _mapping.WriteOutputAsync(signal, newState);
            if (item != null) item.IsHigh = newState;
        }

        private void ResetToDefault()
        {
            foreach (var item in InputItems)
            {
                if (Enum.TryParse<AllInputs>(item.SignalName, out var signal))
                    item.BitNo = (int)signal;
            }
            foreach (var item in OutputItems)
            {
                if (Enum.TryParse<AllOutputs>(item.SignalName, out var signal))
                    item.BitNo = (int)signal;
            }
        }

        public void Dispose()
        {
            if (_monitor != null)
                _monitor.InputChanged -= OnInputChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
