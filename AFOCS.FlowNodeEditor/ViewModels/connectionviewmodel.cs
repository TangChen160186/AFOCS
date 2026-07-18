using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AFOCS.FlowNodeEditor.ViewModels
{
    /// <summary>
    /// 连接 ViewModel —— NodifyEditor.Connections 中的每一项
    /// Output = 源连接器（输出端口）, Input = 目标连接器（输入端口）
    /// </summary>
    public class ConnectionViewModel : INotifyPropertyChanged
    {
        private ConnectorViewModel _output;
        private ConnectorViewModel _input;

        public ConnectionViewModel(ConnectorViewModel output, ConnectorViewModel input)
        {
            _output = output;
            _input = input;
            output.IsConnected = true;
            input.IsConnected = true;
        }

        public ConnectorViewModel Output
        {
            get => _output;
            set { _output = value; Notify(); }
        }

        public ConnectorViewModel Input
        {
            get => _input;
            set { _input = value; Notify(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
