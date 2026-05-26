using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using static MiniPreview.Windows.NativeMethods;

namespace MiniPreview.Windows;

public interface IWindowProvider
{
    IEnumerable<IntPtr> GetTopLevelHandles();
    bool IsVisible(IntPtr hwnd);
    string GetTitle(IntPtr hwnd);
    uint GetProcessId(IntPtr hwnd);
    string GetProcessName(uint pid);
    bool IsToolWindow(IntPtr hwnd);
    string? GetProcessExePath(uint pid);
}

public sealed class Win32WindowProvider : IWindowProvider
{
    public IEnumerable<IntPtr> GetTopLevelHandles()
    {
        var list = new List<IntPtr>();
        EnumWindows((h, _) => { list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }

    public bool IsVisible(IntPtr hwnd) => IsWindowVisible(hwnd);

    public string GetTitle(IntPtr hwnd)
    {
        var len = GetWindowTextLength(hwnd);
        if (len <= 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public uint GetProcessId(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    public string GetProcessName(uint pid)
    {
        try { return Process.GetProcessById((int)pid).ProcessName; }
        catch { return "(unknown)"; }
    }

    public bool IsToolWindow(IntPtr hwnd)
    {
        var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        return (ex & WS_EX_TOOLWINDOW) != 0 && (ex & WS_EX_APPWINDOW) == 0;
    }

    public string? GetProcessExePath(uint pid)
    {
        try { return Process.GetProcessById((int)pid).MainModule?.FileName; }
        catch { return null; }
    }
}

public sealed class WindowEnumerator
{
    private readonly IWindowProvider _provider;

    public WindowEnumerator() : this(new Win32WindowProvider()) { }
    public WindowEnumerator(IWindowProvider provider) => _provider = provider;

    public IEnumerable<WindowInfo> EnumerateVisibleWindows()
    {
        foreach (var hwnd in _provider.GetTopLevelHandles())
        {
            if (!_provider.IsVisible(hwnd)) continue;
            var title = _provider.GetTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title)) continue;
            if (_provider.IsToolWindow(hwnd)) continue;

            var pid = _provider.GetProcessId(hwnd);
            var procName = _provider.GetProcessName(pid);
            var icon = TryLoadIcon(_provider.GetProcessExePath(pid));

            yield return new WindowInfo
            {
                Handle = hwnd,
                Title = title,
                ProcessId = pid,
                ProcessName = procName,
                Icon = icon
            };
        }
    }

    private static System.Windows.Media.ImageSource? TryLoadIcon(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return null;
        var info = new SHFILEINFO();
        var size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(info);
        var ptr = SHGetFileInfo(exePath, 0, ref info, size, SHGFI_ICON | SHGFI_SMALLICON);
        if (ptr == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;

        try
        {
            var bmp = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bmp.Freeze();
            return bmp;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }
}
