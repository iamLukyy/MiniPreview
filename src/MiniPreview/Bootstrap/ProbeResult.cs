namespace MiniPreview.Bootstrap;

public sealed record ProbeResult(string Name, bool Ok, string Detail, string? FixCommand);
