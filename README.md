# TempTrayWidget — A Tiny, Smooth, Task-Manager-style System Monitor

**Author:** Jake “Dreams” Mayer • **License:** Open source (see `LICENSE`)

TempTrayWidget is a lightweight, always-on-top WPF tray widget that shows CPU/GPU temps and loads with buttery-smooth, Task-Manager-style line charts. It’s designed to be minimal, legible, and snappy—even on busy systems—while offering a clean, modern UI and a small set of practical controls.

---

## ✨ Highlights

* **At-a-glance totals** for CPU & GPU shown side-by-side at the top.
* **Per-core (CPU) and per-engine (GPU) tiles** that *wrap* instead of squish.
* **Smooth “strip chart” animation** that grows on the right and scrolls left.
* **Tray icon** with live tooltip temps (°F).
* **Topmost, compact window** with custom chrome and drag-anywhere title bar.
* **Preferences** page for **accent color** (updates charts, splitters, & border).
* **Safe close behavior** (close hides to tray; Alt+F4 won’t kill your session).
* **Graceful GPU fallback** when per-engine sensors are unavailable.

---

## 🧱 Tech Stack

* **.NET / C#**: WPF app (C# 7.3 compatible; newer versions work too).
* **UI**: WPF (`System.Windows`), custom window chrome.
* **Charts**: \[LiveChartsCore.SkiaSharpView\.WPF] + \[SkiaSharp].
* **Sensors**: \[LibreHardwareMonitor] (CPU/GPU temperature & load sensors).
* **Tray**: \[Hardcodet.Wpf.TaskbarNotification] for system tray integration.

> **NuGet packages (typical)**
> `LiveChartsCore` • `LiveChartsCore.SkiaSharpView` • `LiveChartsCore.SkiaSharpView.WPF` • `SkiaSharp`
> `LibreHardwareMonitorLib` • `Hardcodet.NotifyIcon.Wpf`

---

## 📁 Project Structure (Core Files)

```
TempTrayWidget/
├─ App.xaml.cs                 # App entry; timers, sensors, charts, preferences
├─ WidgetWindow.xaml(.cs)      # Main widget UI (totals + tiles + title bar)
├─ PreferencesWindow.xaml(.cs) # Accent color picker (gear button)
├─ TemperatureMonitor.cs       # Reads CPU/GPU temps (LibreHardwareMonitor)
├─ LoadMonitor.cs              # (Not shown here) Reads CPU/GPU loads & per-core sensors
├─ Assets/                     # Tray icon and any resources
└─ README.md                   # This file
```

---

## 🏗️ How It Works

### 1) Sensor Model

* **TemperatureMonitor** uses **LibreHardwareMonitor** to open the `Computer` and read averaged **CPU** and **GPU** temps across temperature sensors.
* **LoadMonitor** (your implementation) exposes:

  * `ReadLoads()` → `(cpuLoad, gpuLoad)` totals.
  * `GetCpuCoreLoadSensors()` → list of `ISensor` for *per-core* loads.
  * `GetGpuCoreLoadSensors()` → list of `ISensor` for *GPU engines/adapters* (may be empty; we fallback to overall load).

### 2) Update Loop

* A `DispatcherTimer` ticks every **750 ms** (adjustable), then:

  * Reads temps/loads.
  * Updates tray tooltip and header stats.
  * Feeds **totals** and **tiles** using a **sliding-window** algorithm (explained below).

### 3) Smooth “Strip Chart” Animation

The *jumpiness* of reindexing is avoided by never rewriting X indices.

* Each chart series uses **`ObservablePoint(X, Y)`**.
* **Global tick `_tick`** increments on each timer tick.
* For each series we **append** `(X=_tick, Y=value)`, then:

  * slide the visible **X-axis window** to `[_tick - WINDOW, _tick]`
  * occasionally **trim** old points (keep a small buffer beyond the window).

This produces the “growing from the right, disappearing on the left” effect with minimal redraw.

**Core idea (simplified):**

```csharp
void PushPoint(ObservableCollection<ObservablePoint> buf, CartesianChart chart, double y, int window)
{
    _tick++;
    buf.Add(new ObservablePoint(_tick, Clamp01To100(y)));

    // Trim occasionally to avoid unbounded growth
    var minX = _tick - window - 5;
    while (buf.Count > window + 16 && buf[0].X < minX) buf.RemoveAt(0);

    // Slide the window
    var xa = chart.XAxes?.FirstOrDefault();
    if (xa != null) { xa.MinLimit = _tick - window; xa.MaxLimit = _tick; }
}
```

### 4) Layout & Responsiveness

* **Totals at the top**: two bordered charts (CPU | GPU) separated by a vertical splitter.
* **Tiles below**: each section is inside an **Expander** with a **WrapPanel** content.

  * WrapPanel ensures tiles keep a sensible **`MinWidth=320`** and **wrap to new rows** on smaller windows instead of collapsing.

### 5) Preferences (Accent Color)

* Gear button in the title bar opens **PreferencesWindow**.
* User picks an accent; app applies it by:

  * Updating a **DynamicResource** (`AccentBrush`) used by borders & splitters.
  * Re-coloring every **LineSeries** (stroke & fill) across totals and tiles.
* Accent is maintained in both **WPF** (`Color`) and **Skia** (`SKColor`) to keep UI and charts in sync.

---

## 🖥️ Features

* **Always-visible performance snapshot**

  * CPU/GPU totals with percentage gridlines (0–100%).
  * Per-core & per-engine charts for detail.

* **Smooth, readable charts**

  * No markers, straight lines, subtle fill.
  * \~200–220 ms chart animation speed for a steady, modern feel.

* **Polished window chrome**

  * Rounded corners, subtle border, draggable title bar.
  * Minimize/hide behavior tuned for a tray widget.

* **Wrap-based tiles**

  * Tile width is readable (320px), and layout never “squishes”.

* **Practical fallbacks**

  * If per-engine GPU sensors aren’t available, displays a single **GPU** tile fed by total load.
  * If per-core CPU sensors aren’t found, a fallback **CPU (overall)** tile is shown.

* **Theming**

  * Accent colors: Blue (default), Teal, Lime, Purple, Orange, Pink.

---

## 🛠️ Build & Run

### Prerequisites

* Windows with .NET Desktop tooling (Visual Studio or `dotnet` CLI).
* **WPF** support installed.
* NuGet packages:

  * `LiveChartsCore`
  * `LiveChartsCore.SkiaSharpView`
  * `LiveChartsCore.SkiaSharpView.WPF`
  * `SkiaSharp`
  * `LibreHardwareMonitorLib`
  * `Hardcodet.NotifyIcon.Wpf`

### Language Version

* The project is **C# 7.3-compatible**.
* You can upgrade to newer language versions (C# 9+) if you want newer syntax (e.g., target-typed `new`).

### Steps

1. Restore NuGet packages.
2. Build the solution.
3. Run the app; look for the **tray icon**.
4. Click tray menu / open the widget.

   * Use the **gear** icon to change the accent color.
   * Close button hides to tray; right-click tray icon to exit.

> **Note:** LibreHardwareMonitor reads hardware sensors that may vary by vendor/driver. On some systems you’ll see fewer/more per-engine or per-core sensors. The app adapts and falls back gracefully.

---

## ⚙️ Configuration Knobs (quick edits)

* **Update interval**: `DispatcherTimer.Interval` (default: `0.75s`).
* **Window size of visible data**: `WINDOW_SIZE_TOTAL` (default: `120` samples).
* **Tile width**: `MinWidth = 320` and `WrapPanel.ItemWidth = 320` (WPF XAML).
* **Chart feel**: set `AnimationsSpeed` on `LineSeries<ObservablePoint>` to taste.

---

## 🧩 Implementation Notes

* **Chart axes:**

  * X-axis has `MinLimit`/`MaxLimit` updated each tick to create the scroll.
  * Y-axis is fixed 0–100% for consistency across tiles.
* **No point re-indexing:**

  * Avoid `RemoveAt(0)` per tick, which causes jumps.
  * Only trim excess points when the buffer is larger than needed.
* **Per-core & per-engine sensors:**

  * Resolved using sensor name heuristics and types from LibreHardwareMonitor.
  * Vendor differences handled via flexible filtering (your `LoadMonitor`).

---

## 🧪 Known Limitations

* **Sensor availability** differs per machine; some GPUs don’t expose per-engine loads.
* **Admin privileges** normally aren’t required, but some older platforms/drivers may behave differently.
* **Historical persistence**: by design, the app keeps an in-memory rolling window only (no disk history yet).
* **Color preference persistence**: not persisted between runs by default (easy to add; see Roadmap).

---

## 🗺️ Roadmap (Planned / Proposed)

Below is a **Summary Dashboard Comparison** of coming capabilities and what will make them stand out. Items marked **(planned)** are under consideration for the next iterations.

| Feature                                          | What Makes It Unique                                                                                                                               |
| ------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Edge AI Anomaly Detection** (planned)          | Intelligent on-device analysis of load/temp/process metrics with lightweight models; **root-cause suggestions** without sending data to the cloud. |
| **User Activity Playback** (planned)             | **Visual/log-based replay** of user interactions correlated with spikes (privacy-respecting, opt-in).                                              |
| **Low-Resource Safe Mode** (planned)             | **Adaptive, lightweight UI** that auto-triggers under stress to minimize overhead.                                                                 |
| **Deep Process Inspection** (planned)            | Drill into processes: thread stacks, handles, DLLs, **VirusTotal lookups**, memory dumps.                                                          |
| **Distributed Architecture & Embeds** (planned)  | Efficient **edge collection** with sharable, embeddable charts (e.g., in dashboards/OBS overlays).                                                 |
| **Smart Alerts + Suggested Fixes** (planned)     | Contextual alerts with **actionable remedies** (e.g., “GPU at 100% due to encoder—reduce preset”).                                                 |
| **Event & User Session Correlation** (planned)   | Correlate **system events** and **user behavior** with performance changes.                                                                        |
| **Historical Trends & Pattern Alerts** (planned) | Visualize long-term patterns; alert on **recurring “problem hours”** or weekly cycles.                                                             |
| **Automation & Scripting Hooks** (planned)       | Triggers and scripts to **auto-remediate** (kill misbehaving processes, change power plans, etc.).                                                 |

Additional near-term polishing:

* Persist **accent color** and preferences (`Properties.Settings`).
* Expose **update interval** & **tile width** controls.
* Optional **°C/°F** toggle.
* **Hotkeys** for quick show/hide.

---

## 🤝 Contributing

PRs and issues are welcome! Helpful areas:

* Additional sensor providers and heuristics.
* Performance profiling on diverse hardware.
* Accessibility and localization.
* Packaging (MSIX/Winget/Chocolatey).

Please follow conventional C# style and keep the **UI lightweight**.

---

## 🔐 Privacy & Security

* All metrics are read **locally** via LibreHardwareMonitor; nothing is sent anywhere.
* Planned features that interact with external services (e.g., VirusTotal) will be **opt-in** and clearly labeled.

---

## 🙏 Credits

* **Author:** Jake “Dreams” Mayer
* **Charts:** LiveChartsCore + SkiaSharp
* **Sensors:** LibreHardwareMonitor
* **Tray:** Hardcodet NotifyIcon WPF

---

## 🧭 Quick FAQ

**Why not just use Windows Task Manager?**
Task Manager is great. TempTrayWidget focuses on a **compact, always-on-top**, and **customizable** display with a smooth chart aesthetic, per-core tiles that wrap, and a tray-centric workflow.

**The lines still jitter sometimes—what gives?**
Ensure you’re on the **sliding window** build (using `ObservablePoint` and `MinLimit`/`MaxLimit` updates). If your timer is very slow or the system is under extreme load, consider increasing the window size and/or decreasing `AnimationsSpeed`.

**I don’t see per-engine GPU tiles.**
Not all drivers expose them. The app falls back to a single GPU tile fed by overall load.

**How do I change colors?**
Click the **gear** icon in the title bar → **Preferences** → pick an **Accent**.

---

If you ship this in the wild, drop a link—I’d love to see people’s dashboards. 💙
