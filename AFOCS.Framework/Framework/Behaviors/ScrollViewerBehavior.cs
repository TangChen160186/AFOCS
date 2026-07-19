using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AFOCS.Framework.Framework.Behaviors
{
    /// <summary>
    /// ScrollViewer 附加行为：让 ScrollViewer 捕获嵌套控件上的鼠标滚轮事件
    /// 用法：在 ScrollViewer 上设置 behaviors:ScrollViewerBehavior.BubbleMouseWheel="True"
    /// </summary>
    public static class ScrollViewerBehavior
    {
        public static readonly DependencyProperty BubbleMouseWheelProperty =
            DependencyProperty.RegisterAttached(
                "BubbleMouseWheel",
                typeof(bool),
                typeof(ScrollViewerBehavior),
                new PropertyMetadata(false, OnBubbleMouseWheelChanged));

        public static bool GetBubbleMouseWheel(DependencyObject obj)
            => (bool)obj.GetValue(BubbleMouseWheelProperty);

        public static void SetBubbleMouseWheel(DependencyObject obj, bool value)
            => obj.SetValue(BubbleMouseWheelProperty, value);

        private static void OnBubbleMouseWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer scrollViewer) return;

            if ((bool)e.NewValue)
                scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
            else
                scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheel;
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer) return;

            // 向上滚动：Delta > 0，向下滚动：Delta < 0
            var newOffset = scrollViewer.VerticalOffset - e.Delta / 3.0;
            newOffset = Math.Max(0, Math.Min(newOffset, scrollViewer.ScrollableHeight));
            scrollViewer.ScrollToVerticalOffset(newOffset);
            e.Handled = true;
        }
    }
}
