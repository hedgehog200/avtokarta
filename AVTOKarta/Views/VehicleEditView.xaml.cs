using System.Windows;
using AVTOKarta.ViewModels;

namespace AVTOKarta.Views
{
    public partial class VehicleEditView : Window
    {
        public VehicleEditView()
        {
            InitializeComponent();
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
