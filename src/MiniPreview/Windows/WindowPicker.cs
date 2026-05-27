using System.Runtime.InteropServices;
using static MiniPreview.Windows.NativeMethods;

namespace MiniPreview.Windows;

public sealed class WindowPicker : IDisposable
{
    private LowLevelMouseProc? _proc;
    private IntPtr _hook = IntPtr.Zero;
    private Action<IntPtr>? _onPicked;
    private bool _disposed;

    public void BeginPick(Action<IntPtr> onPicked)
    {
        if (_hook != IntPtr.Zero) Cancel();
        _onPicked = onPicked;
        _proc = HookCallback;
        var hModule = GetModuleHandle(null);
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, hModule, 0);
        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException("Nepodařilo se zaregistrovat low-level mouse hook (chyba: " + Marshal.GetLastWin32Error() + ")");
    }

    public void Cancel()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _onPicked = null;
        _proc = null;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var hwnd = WindowFromPoint(data.pt);
            // top-level (project potomky na rodiče)
            var root = GetAncestor(hwnd, GA_ROOT);
            var picked = _onPicked;
            Cancel();
            picked?.Invoke(root);
            return (IntPtr)1; // swallow the click
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Cancel();
        _disposed = true;
    }
}
