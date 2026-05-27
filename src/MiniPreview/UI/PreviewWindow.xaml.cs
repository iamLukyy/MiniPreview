using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MiniPreview.Audio;
using MiniPreview.Capture;
using MiniPreview.Hotkeys;
using MiniPreview.Settings;
using MiniPreview.Windows;

namespace MiniPreview.UI;

public partial class PreviewWindow : Window
{
    private readonly CaptureService _capture = new();
    private readonly AudioMuteService _audio = new();
    private readonly WindowEnumerator _enumerator = new();
    private WindowPicker? _picker;
    private HotkeyManager? _hotkeys;
    private SettingsStore _store = null!;
    private SettingsRoot _settings = null!;
    private WindowInfo? _currentTarget;

    public PreviewWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        _capture.FrameReady += OnFrameReady;
        _capture.TargetClosed += () => Dispatcher.BeginInvoke(() => ShowStatus("Target lost — vyber okno"));
    }

    private void OnFrameReady(CapturedFrame frame)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            var wb = new WriteableBitmap(frame.Width, frame.Height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
            wb.Lock();
            try
            {
                int dstStride = frame.Width * 4;
                if (frame.SourceStride == dstStride)
                {
                    System.Runtime.InteropServices.Marshal.Copy(frame.Bgra, 0, wb.BackBuffer, frame.Bgra.Length);
                }
                else
                {
                    for (int y = 0; y < frame.Height; y++)
                        System.Runtime.InteropServices.Marshal.Copy(frame.Bgra, y * frame.SourceStride, wb.BackBuffer + y * dstStride, dstStride);
                }
                wb.AddDirtyRect(new System.Windows.Int32Rect(0, 0, frame.Width, frame.Height));
            }
            finally { wb.Unlock(); }
            wb.Freeze();
            PreviewImage.Source = wb;
        });
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        _store = app.SettingsStore;
        _settings = app.Settings;

        Left = _settings.Window.X;
        Top = _settings.Window.Y;
        Width = _settings.Window.Width;
        Height = _settings.Window.Height;
        Topmost = _settings.Window.AlwaysOnTop;

        InitHotkeys();
        TryResumeLastTarget();
    }

    private void InitHotkeys()
    {
        _hotkeys = new HotkeyManager();
        try
        {
            var pauseDef = HotkeyDefinition.Parse(_settings.Hotkeys.TogglePause.Modifiers, _settings.Hotkeys.TogglePause.Key);
            _hotkeys.Register(pauseDef, () => Dispatcher.BeginInvoke(TogglePause));
            var muteDef = HotkeyDefinition.Parse(_settings.Hotkeys.ToggleMute.Modifiers, _settings.Hotkeys.ToggleMute.Key);
            _hotkeys.Register(muteDef, () => Dispatcher.BeginInvoke(ToggleMute));
            var pickDef = HotkeyDefinition.Parse(_settings.Hotkeys.OpenPicker.Modifiers, _settings.Hotkeys.OpenPicker.Key);
            _hotkeys.Register(pickDef, () => Dispatcher.BeginInvoke(BeginPickWindow));
        }
        catch (Exception ex)
        {
            ShowStatus("Hotkey selhal: " + ex.Message);
        }
    }

    private void TryResumeLastTarget()
    {
        var last = _settings.Capture.LastTargetProcessName;
        if (string.IsNullOrEmpty(last)) { ShowStatus("Pravým klikem vyber okno"); return; }
        var match = _enumerator.EnumerateVisibleWindows().FirstOrDefault(w => w.ProcessName.Equals(last, StringComparison.OrdinalIgnoreCase));
        if (match == null) { ShowStatus($"'{last}' není spuštěné — pravým vyber jiné"); return; }
        SetTarget(match);
    }

    private void SetTarget(WindowInfo info)
    {
        _currentTarget = info;
        HideStatus();
        _capture.Start(info.Handle, _settings.Capture.Fps);
        _settings.Capture.LastTargetProcessName = info.ProcessName;
        _settings.Capture.LastTargetWindowTitle = info.Title;
        PersistSettings();
    }

    private void ShowStatus(string text)
    {
        StatusText.Text = text;
        StatusOverlay.Visibility = Visibility.Visible;
    }
    private void HideStatus() => StatusOverlay.Visibility = Visibility.Collapsed;

    private void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        WindowsMenu.Items.Clear();
        foreach (var w in _enumerator.EnumerateVisibleWindows())
        {
            var captured = w;
            var item = new MenuItem { Header = $"[{w.ProcessName}] {Truncate(w.Title, 50)}", Tag = w };
            if (w.Icon != null) item.Icon = new System.Windows.Controls.Image { Source = w.Icon, Width = 16, Height = 16 };
            item.Click += (_, _) => SetTarget(captured);
            WindowsMenu.Items.Add(item);
        }
        PauseItem.IsChecked = _capture.IsPaused;
        if (_currentTarget != null)
        {
            MuteItem.IsChecked = _audio.IsMuted(_currentTarget.ProcessId);
        }
        foreach (MenuItem item in FpsMenu.Items)
            item.IsChecked = int.Parse(item.Tag!.ToString()!) == _settings.Capture.Fps;
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";

    private void OnPickWindow(object sender, RoutedEventArgs e) => BeginPickWindow();
    private void BeginPickWindow()
    {
        _picker?.Dispose();
        _picker = new WindowPicker();
        ShowStatus("Klikni na okno které chceš sledovat...");
        _picker.BeginPick(hwnd =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                HideStatus();
                if (hwnd == IntPtr.Zero) return;
                var info = _enumerator.EnumerateVisibleWindows().FirstOrDefault(w => w.Handle == hwnd);
                if (info != null) SetTarget(info);
                else ShowStatus("Nepodařilo se najít vybrané okno");
            });
        });
    }

    private void OnFpsClick(object sender, RoutedEventArgs e)
    {
        var fps = int.Parse(((MenuItem)sender).Tag!.ToString()!);
        _settings.Capture.Fps = fps;
        _capture.SetFps(fps);
        PersistSettings();
    }

    private void OnPauseClick(object sender, RoutedEventArgs e) => TogglePause();
    private void TogglePause()
    {
        if (_capture.IsPaused) { _capture.Resume(); HideStatus(); }
        else { _capture.Pause(); ShowStatus("Paused"); }
        PauseItem.IsChecked = _capture.IsPaused;
    }

    private void OnMuteClick(object sender, RoutedEventArgs e) => ToggleMute();
    private void ToggleMute()
    {
        if (_currentTarget == null) return;
        var muted = _audio.ToggleMute(_currentTarget.ProcessId);
        MuteItem.IsChecked = muted;
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settings) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            PersistSettings();
            Topmost = _settings.Window.AlwaysOnTop;
            _hotkeys?.Dispose();
            InitHotkeys();
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void PersistSettings()
    {
        _settings.Window.X = (int)Left;
        _settings.Window.Y = (int)Top;
        _settings.Window.Width = (int)Width;
        _settings.Window.Height = (int)Height;
        _store.Save(_settings);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        PersistSettings();
        _hotkeys?.Dispose();
        _picker?.Dispose();
        _capture.Dispose();
        _audio.Dispose();
    }
}
