using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using AVTOKarta.Licensing;

namespace AVTOKarta.Views
{
    public partial class LicenseView : Window
    {
        private bool _activated;

        public LicenseView(string reason)
        {
            InitializeComponent();
            MachineCodeBox.Text = LicenseService.GetMachineCode();
            ReasonText.Text = string.IsNullOrWhiteSpace(reason)
                ? "Требуется активация для продолжения работы."
                : reason;
            if (File.Exists(LicenseService.LicenseFilePath))
                LicenseFileBox.Text = LicenseService.LicenseFilePath;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = _activated;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(MachineCodeBox.Text);
            }
            catch (Exception)
            {
                MachineCodeBox.SelectAll();
                MachineCodeBox.Focus();
                return;
            }
            ShowStatus("Код скопирован в буфер обмена.", false);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите файл лицензии",
                Filter = "Файл лицензии (*.lic)|*.lic|Все файлы (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                LicenseFileBox.Text = dialog.FileName;
                if (!_activated)
                {
                    StatusText.Text = string.Empty;
                    InfoTitleText.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_activated)
            {
                DialogResult = true;
                return;
            }

            string source = LicenseFileBox.Text;
            if (string.IsNullOrWhiteSpace(source))
            {
                ShowStatus("Сначала выберите файл лицензии.", true);
                return;
            }

            LicenseInfo info;
            string message;
            if (!LicenseService.TryValidateLicenseFile(source, out info, out message))
            {
                ShowStatus(message, true);
                return;
            }

            string error;
            if (!LicenseService.TryInstallLicenseFile(source, out error))
            {
                ShowStatus(error, true);
                return;
            }

            _activated = true;
            InfoTitleText.Visibility = Visibility.Visible;
            ShowStatus("Лицензия успешно установлена." + Environment.NewLine + LicenseSummary(info), false);
            ActivateButton.Content = "Продолжить";
            ExitButton.Content = "Закрыть";
        }

        private static string LicenseSummary(LicenseInfo info)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(info.Licensee)
                ? "Владелец: не указан. "
                : "Организация: " + info.Licensee + ". ");
            sb.Append(info.IsPerpetual
                ? "Действует: бессрочно."
                : "Действует до: " + info.ExpiresOn + ".");
            sb.Append(" Модули: " + LicenseFeaturesHelper.DisplayList(info.FeatureFlags) + ".");
            return sb.ToString();
        }

        private void ShowStatus(string text, bool isError)
        {
            StatusText.Foreground = (Brush)FindResource(isError ? "DangerBrush" : "SuccessBrush");
            StatusText.Text = text;
        }
    }
}