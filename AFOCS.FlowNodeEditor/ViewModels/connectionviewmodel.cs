using System.ComponentModel;
using System.Runtime.CompilerServices;
using Caliburn.Micro;

namespace AFOCS.FlowNodeEditor.ViewModels
{
    /// <summary>
    /// 连接 ViewModel —— NodifyEditor.Connections 中的每一项
    /// Output = 源连接器（输出端口）, Input = 目标连接器（输入端口）
    /// </summary>
    public class ConnectionViewModel : PropertyChangedBase
    {
        private readonly ConnectorViewModel _output;
        private readonly ConnectorViewModel _input;
        public ConnectorViewModel Output
        {
            get => _output;
            set => Set(ref field, value);
        }

        public ConnectorViewModel Input
        {
            get => _input;
            set => Set(ref field, value);
        }
        public ConnectionViewModel(ConnectorViewModel output, ConnectorViewModel input)
        {
            _output = output;
            _input = input;
            output.IsConnected = true;
            input.IsConnected = true;
        }

      
    }
}
