using System.Windows;

namespace AVTOKarta.Views
{
    public partial class UpdateNotificationWindow : Window
    {
        public bool Confirmed { get; private set; }

        public UpdateNotificationWindow(string remoteVersion, string currentVersion, string notes)
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            VersionBlock.Text = remoteVersion + " (текущая: " + currentVersion + ")";
            NotesBlock.Text = string.IsNullOrEmpty(notes) ? "(нет описания)" : notes;
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            DialogResult = true;
            Close();
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }
    }
}
