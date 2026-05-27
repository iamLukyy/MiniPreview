using System;
using System.IO;
using Xunit;
using MiniPreview.Settings;

namespace MiniPreview.Tests.Settings;

public class SettingsStoreTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MiniPreviewTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileMissing()
    {
        var store = new SettingsStore(_tempDir);
        var settings = store.Load();

        Assert.Equal(1, settings.Version);
        Assert.Equal(5, settings.Capture.Fps);
        Assert.True(settings.Window.AlwaysOnTop);
        Assert.False(settings.Autostart);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new SettingsStore(_tempDir);
        var original = new SettingsRoot
        {
            Window = new WindowSettings { X = 50, Y = 60, Width = 400, Height = 300, AlwaysOnTop = false },
            Capture = new CaptureSettings { Fps = 15, LastTargetProcessName = "chrome.exe", LastTargetWindowTitle = "Test" },
            Autostart = true
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(50, loaded.Window.X);
        Assert.Equal(15, loaded.Capture.Fps);
        Assert.Equal("chrome.exe", loaded.Capture.LastTargetProcessName);
        Assert.True(loaded.Autostart);
    }

    [Fact]
    public void Load_HandlesCorruptFile_BackupsAndReturnsDefaults()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(path, "{ not valid json");

        var store = new SettingsStore(_tempDir);
        var settings = store.Load();

        Assert.Equal(5, settings.Capture.Fps); // defaults
        Assert.True(File.Exists(Path.Combine(_tempDir, "settings.json.bak")), "Backup should exist");
    }
}
