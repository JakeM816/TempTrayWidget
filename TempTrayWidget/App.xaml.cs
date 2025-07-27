using System;
using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;

namespace TempTrayWidget
{
    public partial class App : Application
    {
        private TaskbarIcon _tray;
        private WidgetWindow _widget;
        private DispatcherTimer _timer;
        private TemperatureMonitor _tempMonitor;
        private LoadMonitor _loadMonitor;

        private void OnStartup(object sender, StartupEventArgs e)
        {
            // hide any default MainWindow
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // grab tray icon from resources
            _tray = (TaskbarIcon)FindResource("TrayIcon");

            // init temp reader
            _tempMonitor = new TemperatureMonitor();
            _loadMonitor = new LoadMonitor();
            // timer to update every 2s
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _timer.Tick += (s, _) => UpdateReadings();
            _timer.Start();
            ShowWidget();
        }

        private void UpdateReadings()
        {
            // temperatures (original behavior)
            var (cpuC, gpuC) = _tempMonitor.ReadTemperatures();
            // loads (new)
            var (cpuLoad, gpuLoad) = _loadMonitor.ReadLoads();

            // convert temps to °F
            var cpuF = cpuC * 9f / 5f + 32f;
            var gpuF = gpuC * 9f / 5f + 32f;

            // Tray: only temps
            _tray.ToolTipText =
              $"CPU: {cpuF:F1}°F\n" +
              $"GPU: {gpuF:F1}°F";

            // Desktop widget: full readout
            if (_widget?.IsVisible == true)
            {
                _widget.CpuText.Text = $"CPU Temp:   {cpuF:F1}°F";
                _widget.GpuText.Text = $"GPU Temp:   {gpuF:F1}°F";
                _widget.CpuLoadText.Text = $"CPU Load:   {cpuLoad:F0}%";
                _widget.GpuLoadText.Text = $"GPU Load:   {gpuLoad:F0}%";
            }
        }

        private void ShowWidget()
        {
            if (_widget == null)
            {
                _widget = new WidgetWindow();
                // so we can re‑create it if it's ever closed (hidden)
                _widget.Closed += (_, __) => _widget = null;
            }
            _widget.Show();
        }

        private void ShowWidget_Click(object sender, RoutedEventArgs e)
        {
            if (_widget == null)
            {
                _widget = new WidgetWindow();
                _widget.Closed += (_, __) => _widget = null;
            }
            _widget.Show();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _tray.Dispose();
            Current.Shutdown();
        }
    }
}
