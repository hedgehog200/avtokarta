// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AVTOKarta.Helpers
{
    public static class PasswordInputDialog
    {
        public static string Show(string title, string message)
        {
            var primaryBrush = (Brush)Application.Current.FindResource("PrimaryBrush");
            var successBrush = (Brush)Application.Current.FindResource("SuccessBrush");
            var textPrimary = (Brush)Application.Current.FindResource("TextPrimaryBrush");
            var textSecondary = (Brush)Application.Current.FindResource("TextSecondaryBrush");
            var bgBrush = (Brush)Application.Current.FindResource("BackgroundBrush");
            var dividerBrush = (Brush)Application.Current.FindResource("DividerBrush");
            var borderLight = (Brush)Application.Current.FindResource("BorderLightBrush");

            var window = new Window
            {
                Title = title,
                Width = 500,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = bgBrush
            };

            var outerBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = dividerBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(10)
            };

            var dockPanel = new DockPanel();

            var header = new Border
            {
                Background = primaryBrush,
                CornerRadius = new CornerRadius(12, 12, 0, 0),
                Padding = new Thickness(20, 14, 20, 14)
            };
            var headerText = new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            header.Child = headerText;
            DockPanel.SetDock(header, Dock.Top);
            dockPanel.Children.Add(header);

            var footer = new Border
            {
                BorderBrush = dividerBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 12, 16, 12)
            };
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            string enteredPassword = null;

            var passwordBox = new PasswordBox
            {
                FontSize = 14,
                Height = 36,
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                MinWidth = 90,
                Margin = new Thickness(0, 0, 8, 0),
                FontWeight = FontWeights.SemiBold,
                Background = successBrush,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            okButton.Click += (s, e) =>
            {
                enteredPassword = passwordBox.Password;
                passwordBox.Password = "";
                window.DialogResult = true;
                window.Close();
            };

            var cancelButton = new Button
            {
                Content = "Отмена",
                MinWidth = 90,
                Background = Brushes.Transparent,
                Foreground = textSecondary,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            cancelButton.Click += (s, e) =>
            {
                window.DialogResult = false;
                window.Close();
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            footer.Child = buttonPanel;
            DockPanel.SetDock(footer, Dock.Bottom);
            dockPanel.Children.Add(footer);

            var content = new DockPanel
            {
                Margin = new Thickness(20, 16, 20, 16)
            };

            var msgBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = textPrimary,
                Margin = new Thickness(0, 0, 0, 16)
            };
            DockPanel.SetDock(msgBlock, Dock.Top);
            content.Children.Add(msgBlock);
            content.Children.Add(passwordBox);

            dockPanel.Children.Add(content);
            outerBorder.Child = dockPanel;
            window.Content = outerBorder;

            passwordBox.Focus();
            passwordBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    enteredPassword = passwordBox.Password;
                    passwordBox.Password = "";
                    window.DialogResult = true;
                    window.Close();
                }
            };

            bool? result = window.ShowDialog();
            if (result == true)
                return enteredPassword;

            return null;
        }
    }
}
