// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System.Windows;
using System.Windows.Input;
using AVTOKarta.ViewModels;

namespace AVTOKarta.Views
{
    public partial class SquadEditView : Window
    {
        public SquadEditView()
        {
            InitializeComponent();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SquadEditViewModel;
            if (vm == null) return;

            vm.ConfirmCommand.Execute(null);

            if (vm.DialogResult == true)
            {
                DialogResult = true;
            }
        }
    }
}
