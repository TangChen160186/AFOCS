using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AFOCS.FlowNodeEditor.NodeInspectors
{
    /// <summary>
    /// TextBox 数值输入验证的附加行为，替代 Code-Behind 中的 PreviewTextInput 事件处理。
    /// 参照 AFOCS.Framework.Inspector.Controls.NumericTextBox 的设计思路。
    /// </summary>
    public static class NumericTextBoxHelper
    {
        #region IsIntegerOnly

        public static readonly DependencyProperty IsIntegerOnlyProperty =
            DependencyProperty.RegisterAttached(
                "IsIntegerOnly",
                typeof(bool),
                typeof(NumericTextBoxHelper),
                new PropertyMetadata(false, OnIsIntegerOnlyChanged));

        public static bool GetIsIntegerOnly(DependencyObject obj) =>
            (bool)obj.GetValue(IsIntegerOnlyProperty);

        public static void SetIsIntegerOnly(DependencyObject obj, bool value) =>
            obj.SetValue(IsIntegerOnlyProperty, value);

        private static void OnIsIntegerOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                if ((bool)e.NewValue)
                {
                    textBox.PreviewTextInput += IntegerOnly_PreviewTextInput;
                    DataObject.AddPastingHandler(textBox, IntegerOnly_Pasting);
                }
                else
                {
                    textBox.PreviewTextInput -= IntegerOnly_PreviewTextInput;
                    DataObject.RemovePastingHandler(textBox, IntegerOnly_Pasting);
                }
            }
        }

        private static void IntegerOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^-?\d+$");
        }

        private static void IntegerOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(typeof(string)))
            {
                e.CancelCommand();
                return;
            }
            var text = (string)e.DataObject.GetData(typeof(string));
            if (!Regex.IsMatch(text, @"^-?\d+$"))
                e.CancelCommand();
        }

        #endregion

        #region IsDecimalOnly

        public static readonly DependencyProperty IsDecimalOnlyProperty =
            DependencyProperty.RegisterAttached(
                "IsDecimalOnly",
                typeof(bool),
                typeof(NumericTextBoxHelper),
                new PropertyMetadata(false, OnIsDecimalOnlyChanged));

        public static bool GetIsDecimalOnly(DependencyObject obj) =>
            (bool)obj.GetValue(IsDecimalOnlyProperty);

        public static void SetIsDecimalOnly(DependencyObject obj, bool value) =>
            obj.SetValue(IsDecimalOnlyProperty, value);

        private static void OnIsDecimalOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                if ((bool)e.NewValue)
                {
                    textBox.PreviewTextInput += DecimalOnly_PreviewTextInput;
                    DataObject.AddPastingHandler(textBox, DecimalOnly_Pasting);
                }
                else
                {
                    textBox.PreviewTextInput -= DecimalOnly_PreviewTextInput;
                    DataObject.RemovePastingHandler(textBox, DecimalOnly_Pasting);
                }
            }
        }

        private static void DecimalOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            var fullText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            e.Handled = !Regex.IsMatch(fullText, @"^-?\d*\.?\d*$");
        }

        private static void DecimalOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(typeof(string)))
            {
                e.CancelCommand();
                return;
            }
            if (sender is not TextBox textBox) return;
            var pasteText = (string)e.DataObject.GetData(typeof(string));
            var fullText = textBox.Text.Insert(textBox.CaretIndex, pasteText);
            if (!Regex.IsMatch(fullText, @"^-?\d*\.?\d*$"))
                e.CancelCommand();
        }

        #endregion
    }
}
