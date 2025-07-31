using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace TempTrayWidget
{
    public partial class WidgetWindow : Window
    {
        public event Action ChartReady;
        public WidgetWindow()
        {
            InitializeComponent();
        }

        // Allow dragging the window by its title bar
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        // Minimize to taskbar (it’s hidden from taskbar, so this effectively hides it)
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        // Close should just hide the widget (so you can re-open from the tray)
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        // Override Alt+F4 or other attempts to close
        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;  // cancel the close
            Hide();           // just hide instead
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[WidgetWindow] Window_Loaded event fired. Invoking ChartReady.");
            ChartReady?.Invoke();
        }
    }
}
