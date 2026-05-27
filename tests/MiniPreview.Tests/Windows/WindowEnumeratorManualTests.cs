using MiniPreview.Windows;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace MiniPreview.Tests.Windows;

[Trait("Category", "RequiresDesktop")]
public class WindowEnumeratorManualTests
{
    private readonly ITestOutputHelper _output;
    public WindowEnumeratorManualTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void EnumerateReal_OutputsAtLeastOneWindow()
    {
        var enumerator = new WindowEnumerator();
        var windows = enumerator.EnumerateVisibleWindows().ToList();
        Assert.NotEmpty(windows);
        foreach (var w in windows.Take(10))
            _output.WriteLine($"{w.Handle:X8} [{w.ProcessName}] {w.Title}");
    }
}
