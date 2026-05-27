using System.Diagnostics;
using System.Runtime.InteropServices;
using MiniPreview.Capture;
using Xunit;

namespace MiniPreview.Tests.Capture;

[Trait("Category", "RequiresDesktop")]
public class CaptureServiceTests
{
    // On Windows 11, notepad.exe is a packaged app: Process.Start() launches a broker
    // process whose MainWindowHandle is always 0. The real window appears in a different
    // PID. We find it via FindWindow("Notepad", null) which enumerates the class name.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private static IntPtr WaitForNotepadWindow(int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var hwnd = FindWindow("Notepad", null);
            if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd))
                return hwnd;
            Thread.Sleep(100);
        }
        return IntPtr.Zero;
    }

    [Fact]
    public void Start_AgainstNotepad_DeliversFrameWithin3Seconds()
    {
        var notepad = Process.Start("notepad.exe")!;
        try
        {
            var hwnd = WaitForNotepadWindow(5000);
            Assert.NotEqual(IntPtr.Zero, hwnd);

            using var capture = new CaptureService();
            var frameSignal = new ManualResetEventSlim();
            capture.FrameReady += _ => frameSignal.Set();

            capture.Start(hwnd, fps: 10);
            Assert.True(frameSignal.Wait(TimeSpan.FromSeconds(3)), "Žádný frame nedoručen do 3 s");
        }
        finally
        {
            try { notepad.Kill(); } catch { }
            // Kill all Notepad instances that may have been spawned
            foreach (var p in Process.GetProcessesByName("Notepad"))
                try { p.Kill(); p.WaitForExit(1000); } catch { }
            foreach (var p in Process.GetProcessesByName("notepad"))
                try { p.Kill(); p.WaitForExit(1000); } catch { }
        }
    }

    [Fact]
    public void Pause_StopsFrames()
    {
        var notepad = Process.Start("notepad.exe")!;
        try
        {
            var hwnd = WaitForNotepadWindow(5000);
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
            try { notepad.Kill(); } catch { }
            foreach (var p in Process.GetProcessesByName("Notepad"))
                try { p.Kill(); p.WaitForExit(1000); } catch { }
            foreach (var p in Process.GetProcessesByName("notepad"))
                try { p.Kill(); p.WaitForExit(1000); } catch { }
        }
    }
}
