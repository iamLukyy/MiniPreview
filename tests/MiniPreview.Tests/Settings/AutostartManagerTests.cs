using MiniPreview.Settings;
using Microsoft.Win32;
using Xunit;

namespace MiniPreview.Tests.Settings;

public class AutostartManagerTests : IDisposable
{
    private const string TestSubKey = @"Software\MiniPreviewTests\Run";
    private readonly AutostartManager _mgr;

    public AutostartManagerTests()
    {
        _mgr = new AutostartManager(TestSubKey, "MiniPreviewTest", @"C:\fake\MiniPreview.exe");
        Cleanup();
    }

    public void Dispose() => Cleanup();

    private static void Cleanup()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\MiniPreviewTests", false); } catch { }
    }

    [Fact]
    public void IsEnabled_FalseInitially()
    {
        Assert.False(_mgr.IsEnabled);
    }

    [Fact]
    public void Enable_ThenIsEnabled()
    {
        _mgr.Enable();
        Assert.True(_mgr.IsEnabled);
    }

    [Fact]
    public void Disable_ThenIsNotEnabled()
    {
        _mgr.Enable();
        _mgr.Disable();
        Assert.False(_mgr.IsEnabled);
    }

    [Fact]
    public void Enable_StoresExePath()
    {
        _mgr.Enable();
        using var key = Registry.CurrentUser.OpenSubKey(TestSubKey);
        Assert.Equal(@"C:\fake\MiniPreview.exe", key?.GetValue("MiniPreviewTest"));
    }
}
