using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using MiniPreview.Hotkeys;
using MiniPreview.Localization;
using MiniPreview.Settings;

namespace MiniPreview.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsRoot _settings;
    private readonly AutostartManager _autostart = new();
    private readonly string _originalLanguage;

    public SettingsWindow(SettingsRoot settings)
    {
        InitializeComponent();
        _settings = settings;
        _originalLanguage = settings.Language;

        ApplyLocalization();

        LanguageBox.ItemsSource = Strings.SupportedLanguages
            .Select(l => new { Code = l.Code, DisplayName = l.DisplayName })
            .ToArray();
        LanguageBox.SelectedValue = _settings.Language;

        PauseHotkey.Text  = FormatBinding(_settings.Hotkeys.TogglePause);
        MuteHotkey.Text   = FormatBinding(_settings.Hotkeys.ToggleMute);
        PickerHotkey.Text = FormatBinding(_settings.Hotkeys.OpenPicker);
        AutostartBox.IsChecked = _autostart.IsEnabled;
        AlwaysOnTopBox.IsChecked = _settings.Window.AlwaysOnTop;
    }

    private void ApplyLocalization()
    {
        Title                  = Strings.T("settings.title");
        LanguageGroup.Header   = Strings.T("settings.language");
        LanguageHint.Text      = Strings.T("settings.languageHint");
        HotkeysGroup.Header    = Strings.T("settings.hotkeys");
        PauseLabel.Text        = Strings.T("settings.hotkeyPause");
        MuteLabel.Text         = Strings.T("settings.hotkeyMute");
        PickerLabel.Text       = Strings.T("settings.hotkeyPicker");
        HotkeyHint.Text        = Strings.T("settings.hotkeyFormat");
        AutostartBox.Content   = Strings.T("settings.autostart");
        AlwaysOnTopBox.Content = Strings.T("settings.alwaysOnTop");
        OkBtn.Content          = Strings.T("settings.ok");
        CancelBtn.Content      = Strings.T("settings.cancel");
    }

    private static string FormatBinding(HotkeyBinding b) => string.Join("+", b.Modifiers.Append(b.Key));

    private void OnOk(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.Hotkeys.TogglePause = ParseBinding(PauseHotkey.Text);
            _settings.Hotkeys.ToggleMute  = ParseBinding(MuteHotkey.Text);
            _settings.Hotkeys.OpenPicker  = ParseBinding(PickerHotkey.Text);
            HotkeyDefinition.Parse(_settings.Hotkeys.TogglePause.Modifiers, _settings.Hotkeys.TogglePause.Key);
            HotkeyDefinition.Parse(_settings.Hotkeys.ToggleMute.Modifiers, _settings.Hotkeys.ToggleMute.Key);
            HotkeyDefinition.Parse(_settings.Hotkeys.OpenPicker.Modifiers, _settings.Hotkeys.OpenPicker.Key);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Strings.T("settings.invalidHotkey", ex.Message),
                Strings.T("settings.restartTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (AutostartBox.IsChecked == true) _autostart.Enable();
        else _autostart.Disable();
        _settings.Autostart = AutostartBox.IsChecked == true;
        _settings.Window.AlwaysOnTop = AlwaysOnTopBox.IsChecked == true;

        var newLang = (string?)LanguageBox.SelectedValue ?? "en";
        _settings.Language = newLang;
        Strings.Language = newLang;

        DialogResult = true;
        Close();

        if (newLang != _originalLanguage)
        {
            // Some controls cache their text at construction time. Offer to restart.
            var ans = MessageBox.Show(
                Strings.T("settings.restartNow"),
                Strings.T("settings.restartTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ans == MessageBoxResult.Yes)
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exe))
                {
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                    Application.Current.Shutdown();
                }
            }
        }
    }

    private static HotkeyBinding ParseBinding(string s)
    {
        var parts = s.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) throw new ArgumentException(Strings.T("settings.emptyHotkey"));
        return new HotkeyBinding
        {
            Modifiers = parts.Take(parts.Length - 1).ToArray(),
            Key = parts[^1]
        };
    }
}
