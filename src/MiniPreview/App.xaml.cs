using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MiniPreview.Bootstrap;
using MiniPreview.Settings;
using MiniPreview.UI;

namespace MiniPreview;

public partial class App : Application
{
    public SettingsStore SettingsStore { get; private set; } = null!;
    public SettingsRoot Settings { get; private set; } = null!;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MiniPreview", "error.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global unhandled-exception handlers — žádné tiché crashy
        DispatcherUnhandledException += (_, ex) =>
        {
            HandleUnhandled("WPF Dispatcher", ex.Exception);
            ex.Handled = true; // nedopouštět terminate
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            HandleUnhandled("AppDomain", ex.ExceptionObject as Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            HandleUnhandled("Task", ex.Exception);
            ex.SetObserved();
        };

        // Feature check
        var checker = FeatureChecker.CreateDefault();
        var probes = checker.RunProbes();
        if (!FeatureChecker.AllOk(probes))
        {
            var dlg = new FeatureCheckDialog(probes);
            var ok = dlg.ShowDialog();
            if (ok != true) { Shutdown(1); return; }
        }

        // Settings
        SettingsStore = new SettingsStore();
        Settings = SettingsStore.Load();

        // Main window
        var main = new PreviewWindow();
        main.Show();
    }

    private static void HandleUnhandled(string source, Exception? ex)
    {
        if (ex == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex}\n\n");
        }
        catch { /* fallback to MessageBox only */ }

        try
        {
            MessageBox.Show(
                $"Unhandled exception ({source}):\n\n{ex.GetType().Name}: {ex.Message}\n\nFull log: {LogPath}",
                "MiniPreview — chyba", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }
}
