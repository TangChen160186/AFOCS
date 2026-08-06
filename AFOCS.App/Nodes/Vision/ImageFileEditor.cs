using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using Xceed.Wpf.Toolkit.PropertyGrid;
using Xceed.Wpf.Toolkit.PropertyGrid.Editors;

namespace AFOCS.App.Nodes.Vision;

/// <summary>
/// 图片文件选择编辑器：属性面板中显示为文本框 + "…" 按钮，点击弹出图片选择对话框
/// </summary>
public class ImageFileEditor : ITypeEditor
{
    public FrameworkElement ResolveEditor(PropertyItem propertyItem)
    {
        var panel = new DockPanel();

        var textBox = new TextBox
        {
            IsReadOnly = true,
            Background = System.Windows.Media.Brushes.White,
        };
        var binding = new Binding("Value")
        {
            Source = propertyItem,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        };
        textBox.SetBinding(TextBox.TextProperty, binding);

        var button = new Button
        {
            Content = "…",
            Width = 28,
            Height = 20,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 0, 0, 0),
        };
        button.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择图片文件",
                Filter = "图片文件|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有文件|*.*",
            };
            if (dialog.ShowDialog() == true)
            {
                propertyItem.Value = dialog.FileName;
            }
        };

        DockPanel.SetDock(button, Dock.Right);
        panel.Children.Add(button);
        panel.Children.Add(textBox);

        return panel;
    }
}
