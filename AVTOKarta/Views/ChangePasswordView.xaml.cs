// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System.Windows;
using AVTOKarta.ViewModels;

namespace AVTOKarta.Views
{
    public partial class ChangePasswordView : Window
    {
        public ChangePasswordView()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ChangePasswordViewModel;
            if (vm == null) return;

            vm.OldPassword = OldPasswordBox.Password;
            vm.NewPasswordStr = NewPasswordBox.Password;
            vm.ConfirmPassword = ConfirmPasswordBox.Password;

            vm.ConfirmCommand.Execute(null);

            if (vm.DialogResult == true)
            {
                DialogResult = true;
            }
        }
    }
}
