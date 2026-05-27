using MiniPreview.Windows;
using NSubstitute;
using Xunit;

namespace MiniPreview.Tests.Windows;

public class WindowEnumeratorTests
{
    [Fact]
    public void EnumerateVisibleWindows_FiltersInvisible()
    {
        var provider = Substitute.For<IWindowProvider>();
        provider.GetTopLevelHandles().Returns(new[] { (IntPtr)1, (IntPtr)2 });
        provider.IsVisible((IntPtr)1).Returns(true);
        provider.IsVisible((IntPtr)2).Returns(false);
        provider.GetTitle((IntPtr)1).Returns("Notepad");
        provider.GetProcessId((IntPtr)1).Returns(1234u);
        provider.GetProcessName(1234u).Returns("notepad.exe");
        provider.IsToolWindow((IntPtr)1).Returns(false);

        var enumerator = new WindowEnumerator(provider);
        var result = enumerator.EnumerateVisibleWindows().ToList();

        Assert.Single(result);
        Assert.Equal((IntPtr)1, result[0].Handle);
        Assert.Equal("Notepad", result[0].Title);
    }

    [Fact]
    public void EnumerateVisibleWindows_FiltersEmptyTitle()
    {
        var provider = Substitute.For<IWindowProvider>();
        provider.GetTopLevelHandles().Returns(new[] { (IntPtr)1 });
        provider.IsVisible((IntPtr)1).Returns(true);
        provider.GetTitle((IntPtr)1).Returns("");

        var result = new WindowEnumerator(provider).EnumerateVisibleWindows().ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void EnumerateVisibleWindows_FiltersToolWindows()
    {
        var provider = Substitute.For<IWindowProvider>();
        provider.GetTopLevelHandles().Returns(new[] { (IntPtr)1 });
        provider.IsVisible((IntPtr)1).Returns(true);
        provider.GetTitle((IntPtr)1).Returns("Tool");
        provider.GetProcessId((IntPtr)1).Returns(99u);
        provider.GetProcessName(99u).Returns("any.exe");
        provider.IsToolWindow((IntPtr)1).Returns(true);

        var result = new WindowEnumerator(provider).EnumerateVisibleWindows().ToList();
        Assert.Empty(result);
    }
}
