using System;

namespace MiniPreview.Settings;

public sealed class SettingsRoot
{
    public int Version { get; set; } = 1;
    public WindowSettings Window { get; set; } = new();
    public CaptureSettings Capture { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public bool Autostart { get; set; }
}

public sealed class WindowSettings
{
    public int X { get; set; } = 100;
    public int Y { get; set; } = 100;
    public int Width { get; set; } = 320;
    public int Height { get; set; } = 240;
    public bool AlwaysOnTop { get; set; } = true;
}

public sealed class CaptureSettings
{
    public int Fps { get; set; } = 5;
    public string? LastTargetProcessName { get; set; }
    public string? LastTargetWindowTitle { get; set; }
}

public sealed class HotkeySettings
{
    public HotkeyBinding TogglePause { get; set; } = new() { Modifiers = new[] { "Ctrl", "Alt" }, Key = "P" };
    public HotkeyBinding ToggleMute  { get; set; } = new() { Modifiers = new[] { "Ctrl", "Alt" }, Key = "M" };
    public HotkeyBinding OpenPicker  { get; set; } = new() { Modifiers = new[] { "Ctrl", "Alt" }, Key = "L" };
}

public sealed class HotkeyBinding
{
    public string[] Modifiers { get; set; } = Array.Empty<string>();
    public string Key { get; set; } = "";
}
