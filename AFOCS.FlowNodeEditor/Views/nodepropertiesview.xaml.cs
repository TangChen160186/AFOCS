using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using AFOCS.FlowNodeEditor.ViewModels;

namespace AFOCS.FlowNodeEditor.Views
{
    public partial class NodePropertiesView : UserControl
    {
        public NodePropertiesView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldVm)
            {
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            }

            if (e.NewValue is INotifyPropertyChanged newVm)
            {
                newVm.PropertyChanged += OnViewModelPropertyChanged;
                UpdatePropertyGridSelectedObject(newVm);
            }
            else
            {
                propertyGrid.SelectedObject = null;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NodeEditorDocumentViewModel.SelectedNode))
            {
                UpdatePropertyGridSelectedObject(sender);
            }
        }

        private void UpdatePropertyGridSelectedObject(object? dataContext)
        {
            if (dataContext is NodeEditorDocumentViewModel vm && vm.SelectedNode != null)
            {
                propertyGrid.SelectedObject = vm.SelectedNode.Definition;
            }
            else
            {
                propertyGrid.SelectedObject = null;
            }
        }
    }
}