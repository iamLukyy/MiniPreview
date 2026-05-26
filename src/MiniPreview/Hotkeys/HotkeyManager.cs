using System.Runtime.InteropServices;
using System.Windows.Interop;
using static MiniPreview.Windows.NativeMethods;

namespace MiniPreview.Hotkeys;

public sealed class HotkeyManager : IDisposable
{
    private readonly Dictionary<int, Action> _actions = new();
    private readonly HwndSource _source;
    private int _nextId = 1;
    private bool _disposed;

    public HotkeyManager()
    {
        // message-only HWND (HWND_MESSAGE = -3)
        var parameters = new HwndSourceParameters("MiniPreviewHotkeyWindow")
        {
            ParentWindow = new IntPtr(-3),
            HwndSourceHook = WndProc
        };
        _source = new HwndSource(parameters);
    }

    public int Register(HotkeyDefinition def, Action action)
    {
        var id = _nextId++;
        var modsNoRepeat = def.Modifiers | MOD_NOREPEAT;
        if (!RegisterHotKey(_source.Handle, id, modsNoRepeat, def.VirtualKey))
            throw new InvalidOperationException($"RegisterHotKey selhal (mods=0x{def.Modifiers:X}, vk=0x{def.VirtualKey:X}). Win32 error: {Marshal.GetLastWin32Error()}");
        _actions[id] = action;
        return id;
    }

    public void Unregister(int id)
    {
        if (_actions.Remove(id))
            UnregisterHotKey(_source.Handle, id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys.ToList())
            Unregister(id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_actions.TryGetValue(id, out var action))
            {
                action();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        UnregisterAll();
        _source.Dispose();
        _disposed = true;
    }
}
