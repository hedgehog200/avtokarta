// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Windows;
using System.Windows.Controls;

namespace AVTOKarta.Helpers
{
    public static class PhoneHelper
    {
        public static readonly DependencyProperty EnablePhoneFormatProperty =
            DependencyProperty.RegisterAttached(
                "EnablePhoneFormat",
                typeof(bool),
                typeof(PhoneHelper),
                new PropertyMetadata(false, OnEnablePhoneFormatChanged));

        public static void SetEnablePhoneFormat(DependencyObject obj, bool value)
            => obj.SetValue(EnablePhoneFormatProperty, value);

        public static bool GetEnablePhoneFormat(DependencyObject obj)
            => (bool)obj.GetValue(EnablePhoneFormatProperty);

        private static void OnEnablePhoneFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox && (bool)e.NewValue)
            {
                textBox.TextChanged += PhoneTextBox_TextChanged;
                textBox.GotFocus += PhoneTextBox_GotFocus;
            }
        }

        private static void PhoneTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrEmpty(textBox.Text))
            {
                textBox.Text = "+7 (";
                textBox.CaretIndex = textBox.Text.Length;
            }
        }

        private static void PhoneTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                FormatPhoneNumber(textBox);
            }
        }

        private static void FormatPhoneNumber(TextBox textBox)
        {
            textBox.TextChanged -= PhoneTextBox_TextChanged;

            int caretIndex = textBox.CaretIndex;
            int textLenBefore = textBox.Text.Length;

            string digits = ExtractDigits(textBox.Text);

            if (digits.Length == 0)
            {
                textBox.Text = string.Empty;
                textBox.CaretIndex = 0;
                textBox.TextChanged += PhoneTextBox_TextChanged;
                return;
            }

            if (digits.Length > 11)
                digits = digits.Substring(0, 11);

            string formatted = FormatDigits(digits);
            textBox.Text = formatted;

            int delta = formatted.Length - textLenBefore;
            textBox.CaretIndex = Math.Max(0, Math.Min(formatted.Length, caretIndex + delta));

            textBox.TextChanged += PhoneTextBox_TextChanged;
        }

        private static string ExtractDigits(string text)
        {
            string result = string.Empty;
            foreach (char c in text)
            {
                if (char.IsDigit(c))
                    result += c;
            }
            return result;
        }

        private static string FormatDigits(string digits)
        {
            if (digits.Length == 0)
                return string.Empty;

            string d = digits;

            if (d.Length <= 1)
                return "+" + d;

            if (d.Length <= 4)
                return "+" + d[0] + " (" + d.Substring(1);

            if (d.Length <= 7)
                return "+" + d[0] + " (" + d.Substring(1, 3) + ") " + d.Substring(4);

            if (d.Length <= 9)
                return "+" + d[0] + " (" + d.Substring(1, 3) + ") " + d.Substring(4, 3) + "-" + d.Substring(7);

            return "+" + d[0] + " (" + d.Substring(1, 3) + ") " + d.Substring(4, 3) + "-" + d.Substring(7, 2) + "-" + d.Substring(9);
        }
    }
}
