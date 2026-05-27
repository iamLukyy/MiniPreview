namespace MiniPreview.Hotkeys;

public sealed record HotkeyDefinition(uint Modifiers, uint VirtualKey)
{
    private static readonly Dictionary<string, uint> ModMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["alt"] = 0x0001,
        ["ctrl"] = 0x0002,
        ["control"] = 0x0002,
        ["shift"] = 0x0004,
        ["win"] = 0x0008,
        ["super"] = 0x0008,
    };

    public static HotkeyDefinition Parse(IEnumerable<string> modifiers, string key)
    {
        uint mods = 0;
        foreach (var m in modifiers)
        {
            if (!ModMap.TryGetValue(m, out var flag))
                throw new ArgumentException($"Neznámý modifier: {m}", nameof(modifiers));
            mods |= flag;
        }

        var vk = ParseKey(key);
        return new HotkeyDefinition(mods, vk);
    }

    private static uint ParseKey(string key)
    {
        var k = key.Trim();
        if (k.Length == 1)
        {
            var c = char.ToUpperInvariant(k[0]);
            if (c >= 'A' && c <= 'Z') return c;
            if (c >= '0' && c <= '9') return c;
            throw new ArgumentException($"Neznámá klávesa: {key}", nameof(key));
        }
        // F1-F24
        if (k.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(k.Substring(1), out var fn) && fn >= 1 && fn <= 24)
            return (uint)(0x70 + (fn - 1)); // VK_F1 = 0x70
        throw new ArgumentException($"Neznámá klávesa: {key}", nameof(key));
    }
}
