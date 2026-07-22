using System.Windows;
using AVTOKarta.ViewModels;

namespace AVTOKarta.Views
{
    public partial class SquadEditView : Window
    {
        public SquadEditView()
        {
            InitializeComponent();
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
