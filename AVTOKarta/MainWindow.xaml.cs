// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AVTOKarta.ViewModels;

namespace AVTOKarta
{
    public partial class MainWindow : Window
    {
        private bool _isSidebarCollapsed;
        private const double SidebarExpandedWidth = 220;
        private const double SidebarCollapsedWidth = 56;
        private const int AnimationSteps = 12;
        private const int AnimationIntervalMs = 16;
        private DispatcherTimer _sidebarAnimationTimer;

        private TextBlock[] _navLabels;
        private Button[] _navButtons;
        private UIElement[] _collapsibleElements;
        private UIElement[] _dividers;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            _navLabels = new TextBlock[] { NavLabel1, NavLabel2, NavLabel3, NavLabel4, NavLabel5, NavLabel6, NavLabel7, NavLabel8, NavLabel9, NavLabel10 };
            _navButtons = new Button[] { NavBtn1, NavBtn2, NavBtn3, NavBtn4, NavBtn5, NavBtn6, NavBtn7, NavBtn8, NavBtn9, NavBtn10 };
            _collapsibleElements = new UIElement[] { SidebarLogo, SidebarSquadSelector, SidebarSquadInfo, SidebarNavLabel, SidebarDataLabel };
            _dividers = new UIElement[] { SidebarDivider1, SidebarDivider2 };
        }

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _isSidebarCollapsed = !_isSidebarCollapsed;
            double targetWidth = _isSidebarCollapsed ? SidebarCollapsedWidth : SidebarExpandedWidth;

            SidebarToggleIcon.Text = _isSidebarCollapsed ? "\u25B6" : "\u276E";

            bool showLabels = !_isSidebarCollapsed;

            foreach (var el in _collapsibleElements)
                el.Visibility = showLabels ? Visibility.Visible : Visibility.Collapsed;

            foreach (var d in _dividers)
                d.Visibility = showLabels ? Visibility.Visible : Visibility.Collapsed;

            foreach (var label in _navLabels)
                label.Visibility = showLabels ? Visibility.Visible : Visibility.Collapsed;

            foreach (var btn in _navButtons)
            {
                if (showLabels)
                {
                    btn.Padding = new Thickness(14, 10, 14, 10);
                    btn.HorizontalContentAlignment = HorizontalAlignment.Left;
                }
                else
                {
                    btn.Padding = new Thickness(0, 10, 0, 10);
                    btn.HorizontalContentAlignment = HorizontalAlignment.Center;
                }
            }

            if (showLabels)
            {
                SidebarNavLabel.HorizontalAlignment = HorizontalAlignment.Left;
                SidebarDataLabel.HorizontalAlignment = HorizontalAlignment.Left;
            }
            else
            {
                SidebarNavLabel.HorizontalAlignment = HorizontalAlignment.Center;
                SidebarDataLabel.HorizontalAlignment = HorizontalAlignment.Center;
            }

            StartSidebarAnimation(targetWidth);
        }

        private void StartSidebarAnimation(double targetWidth)
        {
            if (_sidebarAnimationTimer != null && _sidebarAnimationTimer.IsEnabled)
                _sidebarAnimationTimer.Stop();

            double startWidth = SidebarColumn.Width.Value;
            double diff = targetWidth - startWidth;
            double step = diff / AnimationSteps;
            int frame = 0;

            _sidebarAnimationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(AnimationIntervalMs)
            };

            _sidebarAnimationTimer.Tick += (s, args) =>
            {
                frame++;
                if (frame >= AnimationSteps)
                {
                    SidebarColumn.Width = new GridLength(targetWidth);
                    _sidebarAnimationTimer.Stop();
                }
                else
                {
                    SidebarColumn.Width = new GridLength(startWidth + step * frame);
                }
            };

            _sidebarAnimationTimer.Start();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            vm.HandleKeyDown(e.Key, Keyboard.Modifiers);

            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                e.Handled = true;
            else if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                e.Handled = true;
            else if (e.Key == Key.F5)
                e.Handled = true;
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void GridSplitter_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is GridSplitter gs)
                gs.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
        }

        private void GridSplitter_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is GridSplitter gs)
                gs.Background = System.Windows.Media.Brushes.Transparent;
        }
    }
}
