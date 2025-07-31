using System;
using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using LibreHardwareMonitor.Hardware;
using System.Collections.Generic;
using System.Linq;
using LiveChartsCore.Measure;
using System.Diagnostics;
using System.Collections.ObjectModel;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using LiveChartsCore.Kernel;

namespace TempTrayWidget
{
    public partial class App : Application
    {
        // per-core buffers now use ObservableCollection<double>
        private IList<ObservableCollection<double>> _coreBuffers;
        // series should be LineSeries<double>
        private IList<LineSeries<double>> _coreSeries;
        private IList<ISensor> _coreSensors;
        private List<ISeries<double>> _coreISeries;
        private TaskbarIcon _tray;
        private WidgetWindow _widget;
        private DispatcherTimer _timer;
        private TemperatureMonitor _tempMonitor;
        private LoadMonitor _loadMonitor;
        private int _staticDataCounter = 0;
        private Random _random = new Random();
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
                Interval = TimeSpan.FromSeconds(0.75)
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

            // read per‑core loads again
            // push into each ObservableCollection (this will notify the chart)
            for (int i = 0; i < _coreSensors.Count; i++)
            {
                var buf = _coreBuffers[i];
                // drop oldest
                if (buf.Count >= 30) buf.RemoveAt(0);
                // append newest
                buf.Add(_coreSensors[i].Value ?? 0d);
                Debug.WriteLine($"[Debug] Core {i} load: {buf.Last():F1}%");
            }
        }


        private void ShowWidget()
        {
            if (_widget == null)
            {
                _widget = new WidgetWindow();
                // so we can re‑create it if it's ever closed (hidden)
                _widget.Closed += (_, __) => _widget = null;
                // **Attach to Loaded** so the XAML and CpuChart control exist
                InitCoreChart();
            }
            _widget.Show();
        }



        private void ShowWidget_Click(object sender, RoutedEventArgs e)
        {
            ShowWidget();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _tray.Dispose();
            Current.Shutdown();
        }

        // Call this once in OnStartup, after _monitor is initialized:
        private void InitCoreChart()
        {
            _coreSensors = _loadMonitor.GetCpuCoreLoadSensors();

            _coreBuffers = new List<ObservableCollection<double>>();
            _coreSeries = new List<LineSeries<double>>();
            _coreISeries = new List<ISeries<double>>();

            SKColor[] palette = {
                SKColors.Lime,
                SKColors.Cyan,
                SKColors.Magenta,
                SKColors.Orange,
                SKColors.Yellow,
                SKColors.HotPink,
                SKColors.Aqua,
                SKColors.Chartreuse
            };
            List<LineSeries<double>> temp = new List<LineSeries<double>>();
            for (int i = 0; i < _coreSensors.Count; i++)
            {
                double start = 40d;
                var buf = new ObservableCollection<double>(Enumerable.Repeat(start, 2));
                _coreBuffers.Add(buf);

                var series = new LineSeries<double>
                {
                    Values = buf,
                    Name = _coreSensors[i].Name,
                    Stroke = new SolidColorPaint(palette[i % palette.Length], 2),
                    Fill = null,
                    GeometrySize = 0
                };
                _coreSeries.Add(series);
                _coreISeries.Add(series);   
            }

            _widget.CpuChart.Series = _coreSeries;
            _widget.CpuChart.XAxes = new[]
            {
                new Axis
                {
                    Labels      = new string[30],
                    LabelsPaint = white,
                    TicksPaint  = white,
                    NamePaint   = white
                }
            };
            _widget.CpuChart.YAxes = new[]
            {
                new Axis
                {
                    MinLimit = 0, MaxLimit = 100,
                    Labeler           = v => $"{v:F0}%",
                    LabelsPaint       = white,
                    TicksPaint        = white,
                    NamePaint         = white,
                    ShowSeparatorLines = true,
                    SeparatorsPaint     = new SolidColorPaint(SKColors.Gray, 0.5f)  // <-- note singular
                }
            };

        }



    }
}