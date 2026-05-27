using System.Linq;
using System.Windows;
using MiniPreview.Bootstrap;
using MiniPreview.SelfTest;
using MiniPreview.Settings;
using MiniPreview.UI;

namespace MiniPreview;

public partial class App : Application
{
    public SettingsStore SettingsStore { get; private set; } = null!;
    public SettingsRoot Settings { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // --selftest router
        if (e.Args.Length > 0 && e.Args[0] == "--selftest")
        {
            var exitCode = SelfTestRunner.Run();
            Shutdown(exitCode);
            return;
        }

        // Feature check (TODO Task 15: replace MessageBox with FeatureCheckDialog)
        var checker = FeatureChecker.CreateDefault();
        var probes = checker.RunProbes();
        if (!FeatureChecker.AllOk(probes))
        {
            MessageBox.Show("Probe failed:\n" + string.Join("\n", probes.Where(p => !p.Ok).Select(p => $"{p.Name}: {p.Detail}")),
                "MiniPreview", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Settings
        SettingsStore = new SettingsStore();
        Settings = SettingsStore.Load();

        // Main window
        var main = new PreviewWindow();
        main.Show();
    }
}
