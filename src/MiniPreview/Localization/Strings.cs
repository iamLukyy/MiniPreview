using System.Collections.Generic;

namespace MiniPreview.Localization;

/// <summary>Bilingual UI strings. Single source of truth for every user-visible label.</summary>
public static class Strings
{
    private static readonly Dictionary<string, Dictionary<string, string>> Table = new()
    {
        // ---- Toolbar tooltips ----
        ["tooltip.pick"]            = new() { ["en"] = "Select source window (Ctrl+Alt+L)",   ["cs"] = "Vyber zdrojové okno (Ctrl+Alt+L)" },
        ["tooltip.minTarget"]       = new() { ["en"] = "Minimize / restore target (Ctrl+Alt+H)", ["cs"] = "Minimalizuj / obnov cíl (Ctrl+Alt+H)" },
        ["tooltip.minTargetMin"]    = new() { ["en"] = "Minimize target (Ctrl+Alt+H)",         ["cs"] = "Minimalizuj cíl (Ctrl+Alt+H)" },
        ["tooltip.minTargetRestore"]= new() { ["en"] = "Restore target (Ctrl+Alt+H)",          ["cs"] = "Obnov cíl (Ctrl+Alt+H)" },
        ["tooltip.pause"]           = new() { ["en"] = "Pause preview (Ctrl+Alt+P)",           ["cs"] = "Pauza náhledu (Ctrl+Alt+P)" },
        ["tooltip.resume"]          = new() { ["en"] = "Resume preview (Ctrl+Alt+P)",          ["cs"] = "Pokračovat (Ctrl+Alt+P)" },
        ["tooltip.mute"]            = new() { ["en"] = "Mute target (Ctrl+Alt+M)",             ["cs"] = "Ztlumit cíl (Ctrl+Alt+M)" },
        ["tooltip.unmute"]          = new() { ["en"] = "Unmute target (Ctrl+Alt+M)",           ["cs"] = "Zrušit ztlumení (Ctrl+Alt+M)" },
        ["tooltip.fps"]             = new() { ["en"] = "Preview FPS",                          ["cs"] = "FPS náhledu" },
        ["tooltip.settings"]        = new() { ["en"] = "Settings",                             ["cs"] = "Nastavení" },
        ["tooltip.close"]           = new() { ["en"] = "Exit",                                 ["cs"] = "Konec" },

        // ---- Status / overlay messages ----
        ["status.selectWindow"]     = new() { ["en"] = "Click the monitor icon above and choose a window", ["cs"] = "Klikni na ikonu monitoru nahoře a vyber okno" },
        ["status.targetNotRunning"] = new() { ["en"] = "'{0}' is not running — pick another window", ["cs"] = "'{0}' neběží — vyber jiné okno" },
        ["status.targetLost"]       = new() { ["en"] = "Target lost — pick a window",          ["cs"] = "Cíl ztracen — vyber okno" },
        ["status.captureFailed"]    = new() { ["en"] = "Capture failed: {0}",                  ["cs"] = "Capture selhal: {0}" },
        ["status.clickPick"]        = new() { ["en"] = "Click on the window you want to track...", ["cs"] = "Klikni na okno které chceš sledovat..." },
        ["status.pickNotFound"]     = new() { ["en"] = "Could not locate the selected window", ["cs"] = "Nepodařilo se najít vybrané okno" },
        ["status.noAudio"]          = new() { ["en"] = "This app is not playing any audio right now", ["cs"] = "Tato aplikace momentálně nepřehrává zvuk" },
        ["status.hotkeyFailed"]     = new() { ["en"] = "Hotkey registration failed: {0}",      ["cs"] = "Registrace klávesové zkratky selhala: {0}" },
        ["status.pickButton"]       = new() { ["en"] = "Pick a window",                        ["cs"] = "Vybrat okno" },

        // ---- Picker popup ----
        ["picker.clickToPick"]      = new() { ["en"] = "Click on a window...",                 ["cs"] = "Klikni na okno..." },
        ["picker.fps"]              = new() { ["en"] = "{0} FPS",                              ["cs"] = "{0} FPS" },

        // ---- Settings window ----
        ["settings.title"]          = new() { ["en"] = "MiniPreview — Settings",               ["cs"] = "MiniPreview — Nastavení" },
        ["settings.language"]       = new() { ["en"] = "Language",                             ["cs"] = "Jazyk" },
        ["settings.languageHint"]   = new() { ["en"] = "Restart required after language change.", ["cs"] = "Po změně jazyka je potřeba restartovat aplikaci." },
        ["settings.hotkeys"]        = new() { ["en"] = "Hotkeys",                              ["cs"] = "Klávesové zkratky" },
        ["settings.hotkeyPause"]    = new() { ["en"] = "Pause:",                               ["cs"] = "Pauza:" },
        ["settings.hotkeyMute"]     = new() { ["en"] = "Mute:",                                ["cs"] = "Mute:" },
        ["settings.hotkeyPicker"]   = new() { ["en"] = "Picker:",                              ["cs"] = "Picker:" },
        ["settings.hotkeyFormat"]   = new() { ["en"] = "Format: \"Ctrl+Alt+P\", \"Shift+F12\", etc. Modifiers: Ctrl, Alt, Shift, Win. Key: A-Z, 0-9, F1-F24.", ["cs"] = "Formát: \"Ctrl+Alt+P\", \"Shift+F12\", apod. Modifikátory: Ctrl, Alt, Shift, Win. Klávesa: A-Z, 0-9, F1-F24." },
        ["settings.autostart"]      = new() { ["en"] = "Start with Windows",                   ["cs"] = "Spouštět s Windows" },
        ["settings.alwaysOnTop"]    = new() { ["en"] = "Always on top",                        ["cs"] = "Vždy nahoře" },
        ["settings.ok"]             = new() { ["en"] = "OK",                                   ["cs"] = "OK" },
        ["settings.cancel"]         = new() { ["en"] = "Cancel",                               ["cs"] = "Zrušit" },
        ["settings.invalidHotkey"]  = new() { ["en"] = "Invalid hotkey format: {0}",           ["cs"] = "Chyba ve formátu zkratky: {0}" },
        ["settings.emptyHotkey"]    = new() { ["en"] = "Hotkey cannot be empty",               ["cs"] = "Prázdná zkratka" },
        ["settings.restartNow"]     = new() { ["en"] = "Language changed. Restart MiniPreview now?", ["cs"] = "Jazyk byl změněn. Restartovat MiniPreview teď?" },
        ["settings.restartTitle"]   = new() { ["en"] = "MiniPreview",                          ["cs"] = "MiniPreview" },

        // ---- Feature check dialog ----
        ["featurecheck.title"]      = new() { ["en"] = "MiniPreview — Environment check",      ["cs"] = "MiniPreview — Kontrola prostředí" },
        ["featurecheck.intro"]      = new() { ["en"] = "Some Windows components may be missing. Details:", ["cs"] = "Některé komponenty Windows mohou chybět. Detaily:" },
        ["featurecheck.recheck"]    = new() { ["en"] = "Recheck",                              ["cs"] = "Zkontrolovat znovu" },
        ["featurecheck.tryAnyway"]  = new() { ["en"] = "Try anyway",                           ["cs"] = "Stejně spustit" },
        ["featurecheck.exit"]       = new() { ["en"] = "Exit",                                 ["cs"] = "Konec" },
        ["featurecheck.copy"]       = new() { ["en"] = "Copy",                                 ["cs"] = "Kopírovat" },
        ["featurecheck.copied"]     = new() { ["en"] = "Copied ✓",                             ["cs"] = "Zkopírováno ✓" },

        // ---- App-level error dialog ----
        ["error.title"]             = new() { ["en"] = "MiniPreview — Error",                  ["cs"] = "MiniPreview — Chyba" },
        ["error.body"]              = new() { ["en"] = "Unhandled exception ({0}):\n\n{1}: {2}\n\nFull log: {3}", ["cs"] = "Neošetřená výjimka ({0}):\n\n{1}: {2}\n\nLog: {3}" },
    };

    /// <summary>Current UI language (set once at app start from settings).</summary>
    public static string Language { get; set; } = "en";

    /// <summary>Looks up a string by key. Falls back to English, then to the key itself.</summary>
    public static string T(string key)
    {
        if (Table.TryGetValue(key, out var langs))
        {
            if (langs.TryGetValue(Language, out var v)) return v;
            if (langs.TryGetValue("en", out var en)) return en;
        }
        return key;
    }

    /// <summary>Like T() but with string.Format args.</summary>
    public static string T(string key, params object[] args) => string.Format(T(key), args);

    /// <summary>List of supported languages for the picker.</summary>
    public static readonly (string Code, string DisplayName)[] SupportedLanguages =
    {
        ("en", "English"),
        ("cs", "Čeština"),
    };
}
