using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AVTOKarta.ViewModels;

namespace AVTOKarta.Views
{
    public partial class SquadSetupView : UserControl
    {
        public SquadSetupView()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SquadSetupViewModel;
            if (vm == null) return;

            vm.Password = PasswordBox1.Password;
            vm.ConfirmPassword = PasswordBox2.Password;

            vm.ConfirmCommand.Execute(null);

            if (vm.DialogResult == true)
            {
                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow != null)
                {
                    var mainVm = mainWindow.DataContext as MainViewModel;
                    mainVm?.CompleteSetup(vm.ResultPassword, vm.ResultSquad);
                }
            }
        }
    }
}
