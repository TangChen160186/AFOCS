using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AFOCS.FlowNodeEditor.ViewModels;

namespace AFOCS.FlowNodeEditor.Views
{
    public partial class NodeEditorDocumentView : UserControl
    {
        private NodeEditorDocumentViewModel? _viewModel;
        private Nodify.NodifyEditor? _editor;
        private bool _suppressSelectionSync;

        public NodeEditorDocumentView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is NodeEditorDocumentViewModel oldVm)
            {
                oldVm.Nodes.CollectionChanged -= OnNodesCollectionChanged;
                foreach (var node in oldVm.Nodes)
                    node.PropertyChanged -= OnNodePropertyChanged;
            }

            if (e.NewValue is NodeEditorDocumentViewModel vm)
            {
                _viewModel = vm;
                vm.InitializeToolbox();
                vm.Nodes.CollectionChanged += OnNodesCollectionChanged;
                foreach (var node in vm.Nodes)
                    node.PropertyChanged += OnNodePropertyChanged;
            }
        }

        private void OnNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (NodeViewModel node in e.NewItems)
                    node.PropertyChanged += OnNodePropertyChanged;

            if (e.OldItems != null)
                foreach (NodeViewModel node in e.OldItems)
                    node.PropertyChanged -= OnNodePropertyChanged;
        }

        private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(NodeViewModel.IsSelected)) return;
            if (_suppressSelectionSync || _viewModel == null) return;
            if (sender is not NodeViewModel node) return;

            _suppressSelectionSync = true;
            if (node.IsSelected)
                _viewModel.SelectNode(node);
            else
                _viewModel.DeselectNode(node);
            _suppressSelectionSync = false;
        }

        private void Editor_OnLoaded(object sender, RoutedEventArgs e)
        {
            _editor = sender as Nodify.NodifyEditor;
            if (_editor == null) return;

            // 监听 SelectedItems 的集合变更（支持多选）
            if (_editor.SelectedItems is INotifyCollectionChanged selectedItems)
            {
                selectedItems.CollectionChanged += (_, _) =>
                {
                    if (_viewModel == null || _suppressSelectionSync) return;
                    _suppressSelectionSync = true;
                    _viewModel.ClearSelection();
                    foreach (var item in _editor.SelectedItems)
                    {
                        if (item is NodeViewModel node)
                            _viewModel.SelectNode(node);
                    }
                    _suppressSelectionSync = false;
                };
            }

            // 键盘快捷键
            _editor.PreviewKeyDown += (_, e2) =>
            {
                if (_viewModel == null) return;

                // 复制/粘贴（Ctrl+C / Ctrl+V）
                if (e2.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    _viewModel.CopyNodes();
                    e2.Handled = true;
                }
                else if (e2.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    _viewModel.PasteNodes();
                    e2.Handled = true;
                }
                else if (e2.Key == Key.Delete || e2.Key == Key.Back)
                {
                    if (_viewModel.SelectedNode != null)
                        _viewModel.DeleteSelectedNodeCommand.Execute(null);
                    else if (_viewModel.SelectedConnection != null)
                        _viewModel.DeleteSelectedConnectionCommand.Execute(null);
                    e2.Handled = true;
                }
            };
        }

        /// <summary>
        /// 从工具箱拖入节点到画布
        /// </summary>
        private void Editor_OnDrop(object sender, DragEventArgs e)
        {
            if (_viewModel == null || _editor == null) return;

            if (e.Data.GetDataPresent("ToolboxItem"))
            {
                var item = e.Data.GetData("ToolboxItem") as ToolboxItemViewModel;
                if (item == null) return;

                // 使用 Nodify 内置坐标转换（处理平移和缩放）
                var graphPos = _editor.GetLocationInsideEditor(e);
                var node = item.CreateNodeViewModel();
                node.Location = graphPos;
                _viewModel.Nodes.Add(node);
                _viewModel.IsDirty = true;
            }
        }
    }
}
