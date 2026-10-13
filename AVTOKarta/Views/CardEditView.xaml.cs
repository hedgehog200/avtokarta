// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using AVTOKarta.ViewModels;

namespace AVTOKarta.Views
{
    public partial class CardEditView : Window
    {
        public CardEditView()
        {
            InitializeComponent();
            ClampToWorkArea();
        }

        /// <summary>
        /// Не даём окну выйти за пределы рабочей области экрана
        /// (иначе на мониторах 768/900 px нижняя часть и кнопки обрезаются).
        /// </summary>
        private void ClampToWorkArea()
        {
            var area = SystemParameters.WorkArea;
            double maxH = area.Height - 20;
            double maxW = area.Width - 20;
            if (Height > maxH) Height = maxH;
            if (Width > maxW) Width = maxW;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as CardViewModel;
            if (vm == null) return;

            vm.ConfirmCommand.Execute(null);

            if (vm.DialogResult == true)
            {
                DialogResult = true;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Overlay_Click(object sender, MouseButtonEventArgs e)
        {
            DialogResult = false;
        }

        private void TimeInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9:]$");
        }
    }
}
