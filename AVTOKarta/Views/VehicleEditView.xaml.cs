// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System.Windows;
using System.Windows.Input;
using AVTOKarta.ViewModels;

namespace AVTOKarta.Views
{
    public partial class VehicleEditView : Window
    {
        public VehicleEditView()
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

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as VehicleViewModel;
            if (vm == null) return;

            vm.ConfirmCommand.Execute(null);

            if (vm.DialogResult == true)
            {
                DialogResult = true;
            }
        }
    }
}
