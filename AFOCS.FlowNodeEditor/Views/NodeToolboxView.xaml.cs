using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AFOCS.FlowNodeEditor.ViewModels;
using AFOCS.Framework.Framework;

namespace AFOCS.FlowNodeEditor.Views
{
    /// <summary>
    /// 工具箱视图 —— 支持拖拽节点到编辑器，样式匹配框架 Toolbox
    /// </summary>
    public partial class NodeToolboxView : UserControl
    {
        private bool _draggingItem;
        private Point _mouseStartPosition;

        public NodeToolboxView()
        {
            InitializeComponent();
        }

        private void OnListBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var listBoxItem = VisualTreeUtility.FindParent<ListBoxItem>(
                (DependencyObject)e.OriginalSource);
            _draggingItem = listBoxItem != null;
            _mouseStartPosition = e.GetPosition(ToolboxListBox);

            // 选中点击的项
            if (listBoxItem != null)
                listBoxItem.IsSelected = true;
        }

        private void OnListBoxMouseMove(object sender, MouseEventArgs e)
        {
            if (!_draggingItem)
                return;

            var mousePosition = e.GetPosition(null);
            var diff = _mouseStartPosition - mousePosition;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                var listBoxItem = VisualTreeUtility.FindParent<ListBoxItem>(
                    (DependencyObject)e.OriginalSource);

                if (listBoxItem == null)
                    return;

                var itemViewModel = (ToolboxItemViewModel)ToolboxListBox.ItemContainerGenerator
                    .ItemFromContainer(listBoxItem);

                var dragData = new DataObject("ToolboxItem", itemViewModel);
                DragDrop.DoDragDrop(listBoxItem, dragData, DragDropEffects.Copy);

                _draggingItem = false;
            }
        }
    }
}
