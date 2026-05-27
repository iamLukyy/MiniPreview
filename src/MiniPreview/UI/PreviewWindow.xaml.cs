using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    // Segoe Fluent Icons glyphs (U+E768 Play, U+E769 Pause, U+E767 Volume, U+E74F Mute, U+E921 ChromeMinimize, U+E923 ChromeRestore)
    private const string IconPlay     = "";
    private const string IconPause    = "";
    private const string IconSpeaker  = "";
    private const string IconMute     = "";
    private const string IconMinimize = "";
    private const string IconRestore  = "";

    private readonly CaptureService _capture = new();
    private readonly AudioMuteService _audio = new();
    private readonly WindowEnumerator _enumerator = new();
    private WindowPicker? _picker;
    private HotkeyManager? _hotkeys;
    private SettingsStore _store = null!;
    private SettingsRoot _settings = null!;
    private WindowInfo? _currentTarget;

    private static readonly int[] FpsPresets = { 1, 2, 5, 10, 15, 30 };

    public PreviewWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        _capture.FrameReady += OnFrameReady;
        _capture.TargetClosed += () => Dispatcher.BeginInvoke(() => ShowStatus("Target lost — vyber okno"));
    }

    private void OnTopBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.ChangedButton == MouseButton.Left)
            DragMove();
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
                    Marshal.Copy(frame.Bgra, 0, wb.BackBuffer, frame.Bgra.Length);
                }
                else
                {
                    for (int y = 0; y < frame.Height; y++)
                        Marshal.Copy(frame.Bgra, y * frame.SourceStride, wb.BackBuffer + y * dstStride, dstStride);
                }
                wb.AddDirtyRect(new Int32Rect(0, 0, frame.Width, frame.Height));
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
        FpsBtn.Content = _settings.Capture.Fps.ToString();
        PauseBtn.Content = IconPause;
        MuteBtn.Content = IconSpeaker;
        MinTargetBtn.Content = IconMinimize;

        LoadAppIcon();
        InitHotkeys();
        TryResumeLastTarget();
    }

    private void LoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/MiniPreview.ico", UriKind.Absolute);
            var streamInfo = Application.GetResourceStream(uri);
            if (streamInfo == null) return;
            var decoder = new IconBitmapDecoder(streamInfo.Stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var smallFrame = decoder.Frames.OrderBy(f => Math.Abs(f.PixelWidth - 16)).First();
            var bigFrame   = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
            AppIcon.Source = smallFrame;
            Icon = bigFrame;
        }
        catch { /* no icon, app still works */ }
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
            // Hardcoded for now (no settings entry yet)
            var minDef = HotkeyDefinition.Parse(new[] { "Ctrl", "Alt" }, "H");
            _hotkeys.Register(minDef, () => Dispatcher.BeginInvoke(ToggleMinimizeTarget));
        }
        catch (Exception ex)
        {
            ShowStatus("Hotkey selhal: " + ex.Message);
        }
    }

    private void TryResumeLastTarget()
    {
        var last = _settings.Capture.LastTargetProcessName;
        if (string.IsNullOrEmpty(last)) { ShowStatus("Klikni na ikonu monitoru nahoru a vyber okno"); return; }
        var match = _enumerator.EnumerateVisibleWindows().FirstOrDefault(w => w.ProcessName.Equals(last, StringComparison.OrdinalIgnoreCase));
        if (match == null) { ShowStatus($"'{last}' neni spusteno — vyber jine okno"); return; }
        SetTarget(match);
    }

    private void SetTarget(WindowInfo info)
    {
        _currentTarget = info;
        TargetLabel.Text = $"{info.ProcessName} — {Truncate(info.Title, 40)}";
        HideStatus();
        try
        {
            _capture.Start(info.Handle, _settings.Capture.Fps);
        }
        catch (Exception ex)
        {
            ShowStatus("Capture failed: " + ex.Message);
        }
        _settings.Capture.LastTargetProcessName = info.ProcessName;
        _settings.Capture.LastTargetWindowTitle = info.Title;
        PersistSettings();
        UpdateMuteIcon();
        UpdateMinIcon();
    }

    private void ShowStatus(string text)
    {
        StatusText.Text = text;
        StatusOverlay.Visibility = Visibility.Visible;
    }
    private void HideStatus() => StatusOverlay.Visibility = Visibility.Collapsed;

    private static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";

    // ----- Picker -----

    private void OnPickBtnClick(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        var menu = new ContextMenu { PlacementTarget = btn, Placement = PlacementMode.Bottom };

        var pickItem = new MenuItem { Header = "Klikni na okno..." };
        pickItem.Click += (_, _) => BeginPickWindow();
        menu.Items.Add(pickItem);

        menu.Items.Add(new Separator());

        foreach (var w in _enumerator.EnumerateVisibleWindows())
        {
            var captured = w;
            var item = new MenuItem { Header = $"[{w.ProcessName}] {Truncate(w.Title, 60)}" };
            if (w.Icon != null) item.Icon = new Image { Source = w.Icon, Width = 16, Height = 16 };
            item.Click += (_, _) => SetTarget(captured);
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    private void BeginPickWindow()
    {
        _picker?.Dispose();
        _picker = new WindowPicker();
        ShowStatus("Klikni na okno ktere chces sledovat...");
        _picker.BeginPick(hwnd =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                HideStatus();
                if (hwnd == IntPtr.Zero) return;
                var info = _enumerator.EnumerateVisibleWindows().FirstOrDefault(w => w.Handle == hwnd);
                if (info != null) SetTarget(info);
                else ShowStatus("Nepodarilo se najit vybrane okno");
            });
        });
    }

    // ----- FPS -----

    private void OnFpsBtnClick(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        var menu = new ContextMenu { PlacementTarget = btn, Placement = PlacementMode.Bottom };
        foreach (var fps in FpsPresets)
        {
            var captured = fps;
            var item = new MenuItem { Header = $"{fps} FPS", IsCheckable = true, IsChecked = fps == _settings.Capture.Fps };
            item.Click += (_, _) => SetFps(captured);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void SetFps(int fps)
    {
        _settings.Capture.Fps = fps;
        _capture.SetFps(fps);
        FpsBtn.Content = fps.ToString();
        PersistSettings();
    }

    // ----- Pause -----

    private void OnPauseClick(object sender, RoutedEventArgs e) => TogglePause();
    private void TogglePause()
    {
        if (_capture.IsPaused)
        {
            _capture.Resume();
            HideStatus();
            PauseBtn.Content = IconPause;
            PauseBtn.ToolTip = "Pauza nahledu (Ctrl+Alt+P)";
            PauseOverlay.Visibility = Visibility.Collapsed;
        }
        else
        {
            _capture.Pause();
            PauseBtn.Content = IconPlay;
            PauseBtn.ToolTip = "Play (Ctrl+Alt+P)";
            PauseOverlay.Visibility = Visibility.Visible;
        }
    }

    // ----- Mute -----

    private void OnMuteClick(object sender, RoutedEventArgs e) => ToggleMute();
    private void ToggleMute()
    {
        if (_currentTarget == null) return;
        if (!_audio.HasAudioSession(_currentTarget.ProcessId))
        {
            ShowStatus("Tato appka momentalne neprehrava zvuk");
            return;
        }
        _audio.ToggleMute(_currentTarget.ProcessId);
        UpdateMuteIcon();
    }

    private void UpdateMuteIcon()
    {
        var muted = _currentTarget != null && _audio.IsMuted(_currentTarget.ProcessId);
        if (muted)
        {
            MuteBtn.Content = IconMute;
            MuteBtn.ToolTip = "Unmute target (Ctrl+Alt+M)";
            MuteOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            MuteBtn.Content = IconSpeaker;
            MuteBtn.ToolTip = "Mute target (Ctrl+Alt+M)";
            MuteOverlay.Visibility = Visibility.Collapsed;
        }
    }

    // ----- Minimize / restore target window -----

    private void OnMinTargetClick(object sender, RoutedEventArgs e) => ToggleMinimizeTarget();
    private void ToggleMinimizeTarget()
    {
        if (_currentTarget == null) return;
        var hwnd = _currentTarget.Handle;
        if (NativeMethods.IsIconic(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        else
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, UpdateMinIcon);
    }

    private void UpdateMinIcon()
    {
        if (_currentTarget == null) return;
        if (NativeMethods.IsIconic(_currentTarget.Handle))
        {
            MinTargetBtn.Content = IconRestore;
            MinTargetBtn.ToolTip = "Obnov target (Ctrl+Alt+H)";
        }
        else
        {
            MinTargetBtn.Content = IconMinimize;
            MinTargetBtn.ToolTip = "Minimalizuj target (Ctrl+Alt+H)";
        }
    }

    // ----- Settings -----

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
        if (_store == null || _settings == null) return;
        _settings.Window.X = (int)Left;
        _settings.Window.Y = (int)Top;
        _settings.Window.Width = (int)Width;
        _settings.Window.Height = (int)Height;
        _store.Save(_settings);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        try { PersistSettings(); } catch { }
        try { _hotkeys?.Dispose(); } catch { }
        try { _picker?.Dispose(); } catch { }
        try { _capture.Dispose(); } catch { }
        try { _audio.Dispose(); } catch { }
    }
}
