using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AFOCS.App.Views
{
    public partial class TeachingPointsDocumentView : UserControl
    {
        public TeachingPointsDocumentView()
        {
            InitializeComponent();
        }

        /// <summary>再次点击已选中的示教点时取消选中</summary>
        private void TeachingPointsListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBox listBox) return;

            var hit = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
            var element = hit?.VisualHit;
            while (element != null)
            {
                if (element is ListBoxItem item)
                {
                    if (item.IsSelected)
                    {
                        listBox.SelectedItem = null;
                        e.Handled = true;
                    }
                    return;
                }
                element = VisualTreeHelper.GetParent(element);
            }
        }
    }
}
