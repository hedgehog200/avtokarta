using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AVTOKarta.ViewModels;

namespace AVTOKarta
{
    public partial class MainWindow : Window
    {
        private bool _isDrawerOpen;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            ((MainViewModel)DataContext).PropertyChanged += OnViewModelPropertyChanged;

            DrawerPanel.RenderTransform = new TranslateTransform(-280, 0);
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

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
