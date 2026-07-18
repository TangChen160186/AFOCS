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
            _viewModel.SelectedNode = node.IsSelected ? node : null;
            _suppressSelectionSync = false;
        }

        private void Editor_OnLoaded(object sender, RoutedEventArgs e)
        {
            _editor = sender as Nodify.NodifyEditor;
            if (_editor == null) return;

            if (_editor.SelectedItems is INotifyCollectionChanged selectedItems)
            {
                selectedItems.CollectionChanged += (_, _) =>
                {
                    if (_viewModel == null || _suppressSelectionSync) return;
                    _viewModel.SelectedNode = _editor.SelectedItems.Count > 0
                        ? _editor.SelectedItems[0] as NodeViewModel
                        : null;
                };
            }

            _editor.PreviewKeyDown += (_, e2) =>
            {
                if (_viewModel == null) return;
                if (e2.Key == Key.Delete || e2.Key == Key.Back)
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

                var graphPos = _editor.GetLocationInsideEditor(e);
                _viewModel.AddNodeFromToolbox(item, graphPos);
            }
        }
    }
}
