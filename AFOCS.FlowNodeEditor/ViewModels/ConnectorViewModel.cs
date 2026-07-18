using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using AFOCS.FlowNodeEditor.Models;

namespace AFOCS.FlowNodeEditor.ViewModels
{
    /// <summary>
    /// 连接器 ViewModel —— Nodify 通过 Anchor 属性获取连接点位置
    /// </summary>
    public class ConnectorViewModel : INotifyPropertyChanged
    {
        public string Name { get; }
        public string DisplayName { get; }
        public NodePortType PortType { get; }

        public bool IsInput { get; }
        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected == value) return;
                _isConnected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
            }
        }

        /// <summary>Nodify 自动设置的连接点位置（画布坐标）</summary>
        private Point _anchor;
        public Point Anchor
        {
            get => _anchor;
            set
            {
                if (_anchor == value) return;
                _anchor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Anchor)));
            }
        }

        /// <summary>从属的节点 ViewModel</summary>
        public NodeViewModel Parent { get; }

        public Guid ParentInstanceId => Parent.InstanceId;

        public ConnectorViewModel(NodeViewModel parent, INodePortDefinition portDef, bool isInput)
        {
            Parent = parent;
            Name = portDef.Name;
            DisplayName = portDef.DisplayName;
            PortType = portDef.PortType;
            IsInput = isInput;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
