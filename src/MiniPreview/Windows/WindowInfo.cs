using System.Windows.Media;

namespace MiniPreview.Windows;

public sealed class WindowInfo
{
    public required IntPtr Handle { get; init; }
    public required string Title { get; init; }
    public required uint ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public ImageSource? Icon { get; init; }
}
