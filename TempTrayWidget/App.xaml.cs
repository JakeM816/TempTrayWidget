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
using LiveChartsCore.SkiaSharpView.WPF;
using System.Windows.Controls;
using System.Windows.Media;

namespace TempTrayWidget
{
    public partial class App : Application
    {
        // windowed history length
        private const int WINDOW_SIZE = 60;
        // totals (always visible)
        private const int WINDOW_SIZE_TOTAL = 120;
        private ObservableCollection<double> _cpuTotalBuf;
        private ObservableCollection<double> _gpuTotalBuf;

        // sensors for tiles
        private IList<ISensor> _gpuSensors;

        // per-core/per-gpu tile buffers & charts
        private readonly List<ObservableCollection<double>> _cpuBuffersSmall = new List<ObservableCollection<double>>();
        private readonly List<CartesianChart> _cpuChartsSmall = new List<CartesianChart>();
        private readonly List<ObservableCollection<double>> _gpuBuffersSmall = new List<ObservableCollection<double>>();
        private readonly List<CartesianChart> _gpuChartsSmall = new List<CartesianChart>();

        // smoothed y-axis limits
        private Axis _yAxis;
        private Axis _xAxis;
        private double _yMinSmooth = 0;
        private double _yMaxSmooth = 100;

        // smoothing (0..1), padding, and min span for y-range
        private const double Y_SMOOTH_ALPHA = 0.20;
        private const double Y_PADDING = 5.0;
        private const double Y_MIN_SPAN = 10.0;

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
            // temps + overall loads for text/tray
            var (cpuC, gpuC) = _tempMonitor.ReadTemperatures();
            var (cpuLoad, gpuLoad) = _loadMonitor.ReadLoads();

            var cpuF = cpuC * 9f / 5f + 32f;
            var gpuF = gpuC * 9f / 5f + 32f;

            _tray.ToolTipText = $"CPU: {cpuF:F1}°F\nGPU: {gpuF:F1}°F";

            if (_widget?.IsVisible == true)
            {
                _widget.CpuText.Text = $"CPU Temp:   {cpuF:F1}°F";
                _widget.GpuText.Text = $"GPU Temp:   {gpuF:F1}°F";
                _widget.CpuLoadText.Text = $"CPU Load:   {cpuLoad:F0}%";
                _widget.GpuLoadText.Text = $"GPU Load:   {gpuLoad:F0}%";
            }

            // ---------- feed totals ----------
            if (_cpuTotalBuf != null)
            {
                if (_cpuTotalBuf.Count >= WINDOW_SIZE_TOTAL) _cpuTotalBuf.RemoveAt(0);
                _cpuTotalBuf.Add(Math.Max(0, Math.Min(100, cpuLoad)));
            }

            if (_gpuTotalBuf != null)
            {
                if (_gpuTotalBuf.Count >= WINDOW_SIZE_TOTAL) _gpuTotalBuf.RemoveAt(0);
                _gpuTotalBuf.Add(Math.Max(0, Math.Min(100, gpuLoad)));
            }

            // ---------- feed CPU tiles ----------
            if (_coreSensors != null && _coreSensors.Count > 0
                && _cpuBuffersSmall.Count == _coreSensors.Count)
            {
                for (int i = 0; i < _coreSensors.Count; i++)
                {
                    var buf = _cpuBuffersSmall[i];
                    if (buf.Count >= WINDOW_SIZE_TOTAL) buf.RemoveAt(0);
                    buf.Add(_coreSensors[i].Value ?? 0d);
                }
            }

            // ---------- feed GPU tiles ----------
            if (_gpuSensors != null && _gpuSensors.Count > 0
                && _gpuBuffersSmall.Count == _gpuSensors.Count)
            {
                for (int i = 0; i < _gpuSensors.Count; i++)
                {
                    var buf = _gpuBuffersSmall[i];
                    if (buf.Count >= WINDOW_SIZE_TOTAL) buf.RemoveAt(0);
                    buf.Add(_gpuSensors[i].Value ?? 0d);
                }
            }
            else if (_gpuBuffersSmall.Count == 1)
            {
                // Fallback: one GPU tile fed by overall gpuLoad
                var buf = _gpuBuffersSmall[0];
                if (buf.Count >= WINDOW_SIZE_TOTAL) buf.RemoveAt(0);
                buf.Add(Math.Max(0, Math.Min(100, gpuLoad)));
            }
        }





        private void ShowWidget()
        {
            if (_widget == null)
            {
                _widget = new WidgetWindow();
                _widget.SizeChanged += (_, __) => UpdateTileColumns();
                _widget.Closed += (_, __) => _widget = null;

                // Ensure visual tree is ready
                _widget.Loaded += (_, __) => InitCoreChart();
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

        private void InitCoreChart()
        {
            // sensors
            _coreSensors = _loadMonitor.GetCpuCoreLoadSensors();      // per-core CPU
            _gpuSensors = _loadMonitor.GetGpuCoreLoadSensors();          // per-GPU/engine (if available)
            if (_coreSensors == null) _coreSensors = new List<ISensor>();
            if (_gpuSensors == null) _gpuSensors = new List<ISensor>();

            // ------------- CPU TOTAL -------------
            _cpuTotalBuf = new ObservableCollection<double>(Enumerable.Repeat(0d, WINDOW_SIZE_TOTAL));
            SetupTaskManagerChart(_widget.CpuTotalChart, _cpuTotalBuf, "CPU");

            // ------------- GPU TOTAL -------------
            _gpuTotalBuf = new ObservableCollection<double>(Enumerable.Repeat(0d, WINDOW_SIZE_TOTAL));
            SetupTaskManagerChart(_widget.GpuTotalChart, _gpuTotalBuf, "GPU");

            // ---------- CPU Tiles (per-core) ----------
            BuildPerCoreTilesCpu(_coreSensors);
            _widget.PerCoreGridCpu.Columns = 2; // TM-like

            // ---------- GPU Tiles (per-adapter/engine) ----------
            // If no individual GPU sensors exposed, we’ll build a single tile and feed it with overall gpuLoad.
            BuildPerGpuTiles(_gpuSensors);
            _widget.PerCoreGridGpu.Columns = 2;
        }



   

        private const int WINDOW_SIZE_CORE = 60;

        private void SetupTaskManagerChart(CartesianChart chart, ObservableCollection<double> buffer, string label)
        {
            // teal stroke + subtle fill
            SKColor teal = SKColors.MediumTurquoise;
            var series = new LineSeries<double>
            {
                Values = buffer,
                Name = label,
                Stroke = new SolidColorPaint(teal, 2.5f),
                Fill = new SolidColorPaint(teal.WithAlpha(50)),
                GeometrySize = 0,
                LineSmoothness = 0.0,
                AnimationsSpeed = TimeSpan.FromMilliseconds(180)
            };

            var axisText = new SolidColorPaint(new SKColor(255, 255, 255, 180));
            var gridPaint = new SolidColorPaint(new SKColor(255, 255, 255, 35), 1);

            var x = new Axis
            {
                LabelsPaint = null,
                TicksPaint = null,
                SeparatorsPaint = null,
                MinStep = 1
            };

            var y = new Axis
            {
                MinLimit = 0,
                MaxLimit = 100,
                MinStep = 25,
                Labeler = v => $"{v:F0}%",
                LabelsPaint = axisText,
                TicksPaint = null,
                SeparatorsPaint = gridPaint,
                ShowSeparatorLines = true
            };

            chart.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            chart.LegendPosition = LiveChartsCore.Measure.LegendPosition.Hidden;
            chart.Series = new ISeries[] { series };
            chart.XAxes = new[] { x };
            chart.YAxes = new[] { y };
        }

        private void BuildPerCoreTilesCpu(IList<ISensor> sensors)
        {
            _widget.PerCoreGridCpu.Children.Clear();
            _cpuBuffersSmall.Clear();
            _cpuChartsSmall.Clear();

            var axisText = new SolidColorPaint(new SKColor(255, 255, 255, 160));
            var gridPaint = new SolidColorPaint(new SKColor(255, 255, 255, 35), 1);
            var strokeCol = SKColors.MediumTurquoise;

            for (int idx = 0; idx < sensors.Count; idx++)
            {
                var s = sensors[idx];
                var buf = new ObservableCollection<double>(Enumerable.Repeat(0d, WINDOW_SIZE_TOTAL));
                _cpuBuffersSmall.Add(buf);

                var series = new LineSeries<double>
                {
                    Values = buf,
                    Stroke = new SolidColorPaint(strokeCol, 2f),
                    Fill = new SolidColorPaint(strokeCol.WithAlpha(45)),
                    GeometrySize = 0,
                    LineSmoothness = 0.0,
                    AnimationsSpeed = TimeSpan.FromMilliseconds(150)
                };

                var x = new Axis { LabelsPaint = null, TicksPaint = null, SeparatorsPaint = null, MinStep = 1 };
                var y = new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 100,
                    MinStep = 25,
                    Labeler = v => $"{v:F0}%",
                    LabelsPaint = axisText,
                    TicksPaint = null,
                    SeparatorsPaint = gridPaint,
                    ShowSeparatorLines = true
                };

                var chart = new CartesianChart
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    LegendPosition = LiveChartsCore.Measure.LegendPosition.Hidden,
                    Series = new ISeries[] { series },
                    XAxes = new[] { x },
                    YAxes = new[] { y },
                    Height = 140,
                    Margin = new Thickness(4)
                };

                var container = new Grid { Margin = new Thickness(4) };
                container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                container.Children.Add(new TextBlock
                {
                    Text = s.Name,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    Margin = new Thickness(2, 0, 2, 2)
                });

                Grid.SetRow(chart, 1);
                container.Children.Add(chart);

                _widget.PerCoreGridCpu.Children.Add(container);
                _cpuChartsSmall.Add(chart);
            }
        }

        private void BuildPerGpuTiles(IList<ISensor> sensors)
        {
            _widget.PerCoreGridGpu.Children.Clear();
            _gpuBuffersSmall.Clear();
            _gpuChartsSmall.Clear();

            var axisText = new SolidColorPaint(new SKColor(255, 255, 255, 160));
            var gridPaint = new SolidColorPaint(new SKColor(255, 255, 255, 35), 1);
            var strokeCol = SKColors.MediumTurquoise;

            if (sensors != null && sensors.Count > 0)
            {
                for (int idx = 0; idx < sensors.Count; idx++)
                {
                    var s = sensors[idx];
                    var buf = new ObservableCollection<double>(Enumerable.Repeat(0d, WINDOW_SIZE_TOTAL));
                    _gpuBuffersSmall.Add(buf);

                    var series = new LineSeries<double>
                    {
                        Values = buf,
                        Stroke = new SolidColorPaint(strokeCol, 2f),
                        Fill = new SolidColorPaint(strokeCol.WithAlpha(45)),
                        GeometrySize = 0,
                        LineSmoothness = 0.0,
                        AnimationsSpeed = TimeSpan.FromMilliseconds(150)
                    };

                    var x = new Axis { LabelsPaint = null, TicksPaint = null, SeparatorsPaint = null, MinStep = 1 };
                    var y = new Axis
                    {
                        MinLimit = 0,
                        MaxLimit = 100,
                        MinStep = 25,
                        Labeler = v => $"{v:F0}%",
                        LabelsPaint = axisText,
                        TicksPaint = null,
                        SeparatorsPaint = gridPaint,
                        ShowSeparatorLines = true
                    };

                    var chart = new CartesianChart
                    {
                        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                        LegendPosition = LiveChartsCore.Measure.LegendPosition.Hidden,
                        Series = new ISeries[] { series },
                        XAxes = new[] { x },
                        YAxes = new[] { y },
                        Height = 140,
                        Margin = new Thickness(4)
                    };

                    var container = new Grid { Margin = new Thickness(4) };
                    container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    container.Children.Add(new TextBlock
                    {
                        Text = s.Name,
                        Foreground = Brushes.White,
                        FontSize = 12,
                        Margin = new Thickness(2, 0, 2, 2)
                    });

                    Grid.SetRow(chart, 1);
                    container.Children.Add(chart);

                    _widget.PerCoreGridGpu.Children.Add(container);
                    _gpuChartsSmall.Add(chart);
                }
            }
            else
            {
                // Fallback: create one GPU tile fed by overall gpuLoad from UpdateReadings
                var buf = new ObservableCollection<double>(Enumerable.Repeat(0d, WINDOW_SIZE_TOTAL));
                _gpuBuffersSmall.Add(buf);

                var series = new LineSeries<double>
                {
                    Values = buf,
                    Stroke = new SolidColorPaint(strokeCol, 2f),
                    Fill = new SolidColorPaint(strokeCol.WithAlpha(45)),
                    GeometrySize = 0,
                    LineSmoothness = 0.0,
                    AnimationsSpeed = TimeSpan.FromMilliseconds(150)
                };

                var x = new Axis { LabelsPaint = null, TicksPaint = null, SeparatorsPaint = null, MinStep = 1 };
                var y = new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 100,
                    MinStep = 25,
                    Labeler = v => $"{v:F0}%",
                    LabelsPaint = axisText,
                    TicksPaint = null,
                    SeparatorsPaint = gridPaint,
                    ShowSeparatorLines = true
                };

                var chart = new CartesianChart
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    LegendPosition = LiveChartsCore.Measure.LegendPosition.Hidden,
                    Series = new ISeries[] { series },
                    XAxes = new[] { x },
                    YAxes = new[] { y },
                    Height = 140,
                    Margin = new Thickness(4)
                };

                var container = new Grid { Margin = new Thickness(4) };
                container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                container.Children.Add(new TextBlock
                {
                    Text = "GPU",
                    Foreground = Brushes.White,
                    FontSize = 12,
                    Margin = new Thickness(2, 0, 2, 2)
                });

                Grid.SetRow(chart, 1);
                container.Children.Add(chart);

                _widget.PerCoreGridGpu.Children.Add(container);
                _gpuChartsSmall.Add(chart);
            }
        }

        private void UpdateTileColumns()
        {
            if (_widget == null) return;

            // each tile (container + margins) ~320px wide feels right
            double tileWidth = 320.0;
            int cpuCols = Math.Max(1, (int)Math.Floor((_widget.PerCoreGridCpu.ActualWidth) / tileWidth));
            int gpuCols = Math.Max(1, (int)Math.Floor((_widget.PerCoreGridGpu.ActualWidth) / tileWidth));

            _widget.PerCoreGridCpu.Columns = cpuCols;
            _widget.PerCoreGridGpu.Columns = gpuCols;
        }

    }


}