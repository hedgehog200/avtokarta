using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AVTOKarta.Licensing;
using AVTOKarta.Security;
using AVTOKarta.ViewModels;
using AVTOKarta.Views;

namespace AVTOKarta
{
    public partial class MainWindow : Window
    {
        private bool _isDrawerOpen;
        private DispatcherTimer _guardTimer;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_SETICON = 0x0080;
        private const int ICON_BIG = 1;
        private const int ICON_SMALL = 0;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;

            ((MainViewModel)DataContext).PropertyChanged += OnViewModelPropertyChanged;

            DrawerPanel.RenderTransform = new TranslateTransform(-280, 0);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;

            var iconUri = new Uri("pack://application:,,,/AVTOKarta;component/app.ico", UriKind.Absolute);
            using (var stream = Application.GetResourceStream(iconUri).Stream)
            using (var icon = new System.Drawing.Icon(stream))
            {
                SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, icon.Handle);
                SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, icon.Handle);
            }

            EnsureLicense();
            StartGuard();
        }

        private void StartGuard()
        {
            TamperGuard.TamperDetected += (s, args) =>
            {
                MessageBox.Show(
                    "Обнаружено вмешательство в программу: программа изменена или запущена под отладчиком.\n\n" +
                    "Доступ к расширенным функциям ограничен. " +
                    "Восстановите оригинальные файлы программы и перезапустите её.",
                    "Проверка целостности", MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshLicenseGates();
            };

            _guardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _guardTimer.Tick += (s, args) =>
            {
                LicenseService.EnsureFresh();
                if (TamperGuard.Refresh())
                    RefreshLicenseGates();
            };
            _guardTimer.Start();
        }

        private void EnsureLicense()
        {
            LicenseInfo info;
            string message;
            if (TamperGuard.IsTampered())
            {
                MessageBox.Show(
                    "Программа изменена или работает под отладчиком.\n\n" +
                    "Для продолжения работы восстановите оригинальные файлы " +
                    "и запустите приложение без отладчика.",
                    "Проверка целостности", MessageBoxButton.OK, MessageBoxImage.Warning);
                Application.Current.Shutdown();
                return;
            }

            if (LicenseService.IsLicensed(out info, out message))
            {
                RefreshLicenseGates();
                return;
            }

            var licenseView = new LicenseView(message) { Owner = this };
            if (licenseView.ShowDialog() != true)
            {
                Application.Current.Shutdown();
                return;
            }

            RefreshLicenseGates();
        }

        private void RefreshLicenseGates()
        {
            var vm = DataContext as MainViewModel;
            vm?.RefreshLicenseGates();
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentScreenIndex")
            {
                var vm = (MainViewModel)DataContext;
                UpdateActiveNavButton(vm.CurrentScreenIndex);
            }
        }

        private void UpdateActiveNavButton(int screenIndex)
        {
        }

        private void BurgerBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleDrawer();
        }

        private void OverlayBackdrop_Click(object sender, MouseButtonEventArgs e)
        {
            CloseDrawer();
        }

        private void ToggleDrawer()
        {
            if (_isDrawerOpen)
                CloseDrawer();
            else
                OpenDrawer();
        }

        private void OpenDrawer()
        {
            _isDrawerOpen = true;
            BurgerOverlay.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            OverlayBackdrop.BeginAnimation(OpacityProperty, fadeIn);

            var slideIn = new DoubleAnimation(-280, 0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            DrawerPanel.RenderTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
        }

        private void CloseDrawer()
        {
            _isDrawerOpen = false;

            var slideOut = new DoubleAnimation(0, -280, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            slideOut.Completed += (s, e) =>
            {
                BurgerOverlay.Visibility = Visibility.Collapsed;
            };
            DrawerPanel.RenderTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            OverlayBackdrop.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            vm.HandleKeyDown(e.Key, Keyboard.Modifiers);

            if (e.Key == Key.Escape && _isDrawerOpen)
            {
                CloseDrawer();
                e.Handled = true;
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            using (var vm = DataContext as MainViewModel)
            {
                vm?.Cleanup();
            }
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
