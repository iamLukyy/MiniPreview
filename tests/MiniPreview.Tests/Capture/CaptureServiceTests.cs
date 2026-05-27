using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MiniPreview.Capture;
using Xunit;

namespace MiniPreview.Tests.Capture;

[Trait("Category", "RequiresDesktop")]
public class CaptureServiceTests
{
    // ---------- Win32 helpers ----------

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // ---------- Helpers ----------

    /// <summary>
    /// Creates a uniquely-named temp file, starts Notepad against it, and waits for
    /// a window whose title contains <paramref name="marker"/> to become visible.
    /// Returns (hwnd, tempFilePath, notepadProcess).
    /// </summary>
    private static (IntPtr hwnd, string tempFile, Process process) StartNotepadWithMarker(string marker)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), marker + ".txt");
        File.WriteAllText(tempFile, "");
        var proc = Process.Start("notepad.exe", tempFile)!;
        var hwnd = WaitForWindowByTitleSubstring(marker, 5000);
        return (hwnd, tempFile, proc);
    }

    /// <summary>
    /// Polls via EnumWindows until a visible window whose title contains
    /// <paramref name="titleSubstring"/> is found, or times out.
    /// </summary>
    private static IntPtr WaitForWindowByTitleSubstring(string titleSubstring, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var found = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                var sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, sb.Capacity);
                if (sb.ToString().Contains(titleSubstring, StringComparison.OrdinalIgnoreCase))
                {
                    found = hWnd;
                    return false; // stop enumeration
                }
                return true;
            }, IntPtr.Zero);

            if (found != IntPtr.Zero)
                return found;

            Thread.Sleep(100);
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Kills all Notepad windows whose title contains <paramref name="marker"/> by
    /// resolving the HWND → PID and terminating that process. Also tries the broker
    /// process handle as a fallback. Finally deletes the temp file.
    /// </summary>
    private static void CleanupNotepad(string marker, string tempFile, Process brokerProcess)
    {
        // Find the real Notepad HWND (may differ from broker PID on Win11)
        var hwnd = WaitForWindowByTitleSubstring(marker, 500);
        if (hwnd != IntPtr.Zero)
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != 0)
            {
                try
                {
                    var p = Process.GetProcessById((int)pid);
                    p.Kill();
                    p.WaitForExit(1000);
                }
                catch { /* already gone */ }
            }
        }

        // Fallback: kill the broker process
        try { brokerProcess.Kill(); } catch { }

        // Belt-and-suspenders: kill any remaining Notepad whose title contains the marker
        foreach (var name in new[] { "Notepad", "notepad" })
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try
                {
                    // Use EnumWindows-based check because MainWindowHandle is 0 on Win11
                    var title = p.MainWindowTitle;
                    if (title.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    {
                        p.Kill();
                        p.WaitForExit(1000);
                    }
                }
                catch { }
            }
        }

        // Delete temp file
        try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
    }

    // ---------- Tests ----------

    [Fact]
    public void Start_AgainstNotepad_DeliversFrameWithin3Seconds()
    {
        var marker = "MiniPreviewTest_" + Guid.NewGuid().ToString("N")[..8];
        var (hwnd, tempFile, proc) = StartNotepadWithMarker(marker);
        try
        {
            Assert.NotEqual(IntPtr.Zero, hwnd);

            using var capture = new CaptureService();
            var frameSignal = new ManualResetEventSlim();
            capture.FrameReady += _ => frameSignal.Set();

            capture.Start(hwnd, fps: 10);
            Assert.True(frameSignal.Wait(TimeSpan.FromSeconds(3)), "Žádný frame nedoručen do 3 s");
        }
        finally
        {
            CleanupNotepad(marker, tempFile, proc);
        }
    }

    [Fact]
    public void Pause_StopsFrames()
    {
        var marker = "MiniPreviewTest_" + Guid.NewGuid().ToString("N")[..8];
        var (hwnd, tempFile, proc) = StartNotepadWithMarker(marker);
        try
        {
            Assert.NotEqual(IntPtr.Zero, hwnd);

            using var capture = new CaptureService();
            int frameCount = 0;
            capture.FrameReady += _ => Interlocked.Increment(ref frameCount);

            capture.Start(hwnd, fps: 30);
            Thread.Sleep(500);
            Assert.True(frameCount > 0, "Frames should have arrived before pause");

            capture.Pause();
            var beforePause = frameCount;
            Thread.Sleep(800);
            Assert.Equal(beforePause, frameCount); // no new frames after pause
        }
        finally
        {
            CleanupNotepad(marker, tempFile, proc);
        }
    }
}
