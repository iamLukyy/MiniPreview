using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using MiniPreview.Audio;
using MiniPreview.Bootstrap;
using MiniPreview.Capture;
using MiniPreview.Windows;

namespace MiniPreview.SelfTest;

internal static class SelfTestRunner
{
    public static int Run()
    {
        var probes = FeatureChecker.CreateDefault().RunProbes();
        var probesOk = FeatureChecker.AllOk(probes);

        bool captureOk = false;
        double framesPerSec = 0;
        string? captureError = null;
        try
        {
            var hwnd = FindShellWindow();
            if (hwnd == IntPtr.Zero) throw new InvalidOperationException("Nenalezeno shell okno");

            using var capture = new CaptureService();
            int frameCount = 0;
            capture.FrameReady += _ => Interlocked.Increment(ref frameCount);

            var sw = Stopwatch.StartNew();
            capture.Start(hwnd, fps: 10);

            while (sw.Elapsed < TimeSpan.FromSeconds(2)) Thread.Sleep(50);
            capture.Stop();

            framesPerSec = frameCount / sw.Elapsed.TotalSeconds;
            captureOk = frameCount > 0;
        }
        catch (Exception ex) { captureError = ex.Message; }

        bool audioOk = false;
        string? audioError = null;
        try
        {
            using var audio = new AudioMuteService();
            audioOk = true;
        }
        catch (Exception ex) { audioError = ex.Message; }

        var report = new
        {
            probes,
            probesOk,
            captureOk,
            captureError,
            framesPerSec,
            audioOk,
            audioError
        };
        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return (probesOk && captureOk && audioOk) ? 0 : 1;
    }

    private static IntPtr FindShellWindow()
    {
        var en = new WindowEnumerator();
        foreach (var w in en.EnumerateVisibleWindows())
        {
            if (w.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                return w.Handle;
        }
        return IntPtr.Zero;
    }
}
