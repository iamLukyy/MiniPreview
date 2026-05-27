using System;
using System.Linq;
using System.Windows;
using MiniPreview.Hotkeys;
using MiniPreview.Settings;

namespace MiniPreview.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsRoot _settings;
    private readonly AutostartManager _autostart = new();

    public SettingsWindow(SettingsRoot settings)
    {
        InitializeComponent();
        _settings = settings;

        PauseHotkey.Text = FormatBinding(_settings.Hotkeys.TogglePause);
        MuteHotkey.Text = FormatBinding(_settings.Hotkeys.ToggleMute);
        PickerHotkey.Text = FormatBinding(_settings.Hotkeys.OpenPicker);
        AutostartBox.IsChecked = _autostart.IsEnabled;
        AlwaysOnTopBox.IsChecked = _settings.Window.AlwaysOnTop;
    }

    private static string FormatBinding(HotkeyBinding b) => string.Join("+", b.Modifiers.Append(b.Key));

    private void OnOk(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.Hotkeys.TogglePause = ParseBinding(PauseHotkey.Text);
            _settings.Hotkeys.ToggleMute  = ParseBinding(MuteHotkey.Text);
            _settings.Hotkeys.OpenPicker  = ParseBinding(PickerHotkey.Text);
            // Validate parsable
            HotkeyDefinition.Parse(_settings.Hotkeys.TogglePause.Modifiers, _settings.Hotkeys.TogglePause.Key);
            HotkeyDefinition.Parse(_settings.Hotkeys.ToggleMute.Modifiers, _settings.Hotkeys.ToggleMute.Key);
            HotkeyDefinition.Parse(_settings.Hotkeys.OpenPicker.Modifiers, _settings.Hotkeys.OpenPicker.Key);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Chyba ve formátu hotkey: {ex.Message}", "MiniPreview", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (AutostartBox.IsChecked == true) _autostart.Enable();
        else _autostart.Disable();
        _settings.Autostart = AutostartBox.IsChecked == true;
        _settings.Window.AlwaysOnTop = AlwaysOnTopBox.IsChecked == true;

        DialogResult = true;
        Close();
    }

    private static HotkeyBinding ParseBinding(string s)
    {
        var parts = s.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) throw new ArgumentException("Prázdný hotkey");
        return new HotkeyBinding
        {
            Modifiers = parts.Take(parts.Length - 1).ToArray(),
            Key = parts[^1]
        };
    }
}
