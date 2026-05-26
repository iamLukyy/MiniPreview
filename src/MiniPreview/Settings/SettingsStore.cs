using System;
using System.IO;
using System.Text.Json;

namespace MiniPreview.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _dir;
    private readonly string _filePath;

    public SettingsStore() : this(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\MiniPreview") { }

    public SettingsStore(string dir)
    {
        _dir = dir;
        _filePath = Path.Combine(dir, "settings.json");
    }

    public SettingsRoot Load()
    {
        if (!File.Exists(_filePath)) return new SettingsRoot();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<SettingsRoot>(json, JsonOpts) ?? new SettingsRoot();
        }
        catch (JsonException)
        {
            var backup = _filePath + ".bak";
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(_filePath, backup);
            return new SettingsRoot();
        }
    }

    public void Save(SettingsRoot settings)
    {
        Directory.CreateDirectory(_dir);
        var json = JsonSerializer.Serialize(settings, JsonOpts);
        File.WriteAllText(_filePath, json);
    }
}
