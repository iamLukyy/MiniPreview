using Microsoft.Win32;

namespace MiniPreview.Settings;

public sealed class AutostartManager
{
    private readonly string _subKey;
    private readonly string _valueName;
    private readonly string _exePath;

    public AutostartManager() : this(
        subKey: @"Software\Microsoft\Windows\CurrentVersion\Run",
        valueName: "MiniPreview",
        exePath: System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!)
    { }

    public AutostartManager(string subKey, string valueName, string exePath)
    {
        _subKey = subKey;
        _valueName = valueName;
        _exePath = exePath;
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(_subKey);
            return key?.GetValue(_valueName) is string s && s.Length > 0;
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(_subKey, writable: true);
        key.SetValue(_valueName, _exePath, RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_subKey, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }
}
