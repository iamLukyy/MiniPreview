# MiniPreview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Postavit lehkou WPF aplikaci, která zobrazí floating top-most miniaturní live náhled vybraného okna s pause/resume, nastavitelným FPS a per-process audio mute — bez znatelného FPS hitu ve hrách.

**Architecture:** `Windows.Graphics.Capture` (WGC) běží na samostatném vlákně přes `Direct3D11CaptureFramePool.CreateFreeThreaded`, frames jdou přes Vortice.Direct3D11 do staging textury a do WPF `WriteableBitmap`. Pacing děláme dropováním framů, ne změnou WGC rate (WGC žádný throttle nemá). Audio mute přes NAudio `AudioSessionManager.SimpleAudioVolume.Mute` per PID. Hotkeys přes Win32 `RegisterHotKey` na hidden message-only HWND.

**Tech Stack:**
- .NET 8 (`net8.0-windows10.0.19041.0`, WPF SDK)
- `Microsoft.Windows.CsWinRT` (WinRT projection — WGC)
- `Vortice.Direct3D11` (D3D11 wrapper, single dep)
- `NAudio` (per-process audio)
- Win32 P/Invoke (User32, Shell32, Advapi32)
- xUnit + NSubstitute pro testy

**Locked-in decisions (z otevřených otázek specu):**
1. D3D11 wrapper: **Vortice.Direct3D11** (proti pure P/Invoke — Vortice má managed lifetime, žádný native deps, hotová WGC interop).
2. WPF rendering: **WriteableBitmap** (proti D3DImage — pro thumbnail @ ≤30 FPS dostačující, D3DImage je optimalizace na později).
3. Locale: **CS only** (EN přidat až bude poptávka).

**Repo:** `C:\Users\lukyn\Documents\skripty\MiniPreview` (private GH: `iamLukyy/MiniPreview`, branch `main`).

**Konvence:**
- Všechny path v plánu jsou relativní k repo root (`MiniPreview/`).
- PowerShell commands předpokládají PWD = repo root, PS 5.1 (default Win11), `dotnet` CLI v PATH.
- Commit per task. Style: `feat: ...`, `test: ...`, `chore: ...`, `docs: ...`.
- Žádné `git add .` ani `git add -A` — vždy vyjmenovat soubory.

---

## File Structure

```
MiniPreview/
├── MiniPreview.sln
├── .gitignore
├── Directory.Build.props                    (jednotné settings pro všechny projekty)
├── src/MiniPreview/
│   ├── MiniPreview.csproj
│   ├── App.xaml / App.xaml.cs                (entry point, --selftest router, feature check)
│   ├── Settings/
│   │   ├── Settings.cs                       (data classes: SettingsRoot, WindowSettings, …)
│   │   ├── SettingsStore.cs                  (Load/Save JSON do %APPDATA%)
│   │   └── AutostartManager.cs               (HKCU\…\Run registry)
│   ├── Bootstrap/
│   │   ├── ProbeResult.cs                    (Name, Ok, Detail, FixCommand)
│   │   └── FeatureChecker.cs                 (probe runner)
│   ├── Windows/
│   │   ├── NativeMethods.cs                  (User32/Shell32 P/Invoke)
│   │   ├── WindowInfo.cs                     (HWND, Title, ProcessName, IconSource)
│   │   ├── WindowEnumerator.cs               (EnumWindows + filtr)
│   │   └── WindowPicker.cs                   (LL mouse hook click-to-pick)
│   ├── Hotkeys/
│   │   ├── HotkeyDefinition.cs               (mods + vk)
│   │   └── HotkeyManager.cs                  (message-only HWND + RegisterHotKey)
│   ├── Audio/
│   │   └── AudioMuteService.cs               (NAudio session lookup + mute toggle)
│   ├── Capture/
│   │   ├── IGraphicsCaptureItemInterop.cs    (COM interop interface)
│   │   ├── D3D11Helpers.cs                   (Vortice device, staging texture, copy)
│   │   └── CaptureService.cs                 (WGC lifecycle, pacing, FrameReady)
│   ├── UI/
│   │   ├── PreviewWindow.xaml / .cs          (top-most borderless okno s Image + context menu)
│   │   ├── SettingsWindow.xaml / .cs         (hotkey editor, autostart toggle)
│   │   └── FeatureCheckDialog.xaml / .cs     (probe results UI)
│   └── SelfTest/
│       └── SelfTestRunner.cs                 (--selftest mode, vrátí JSON)
├── tests/MiniPreview.Tests/
│   ├── MiniPreview.Tests.csproj
│   ├── Settings/SettingsStoreTests.cs
│   ├── Settings/AutostartManagerTests.cs
│   ├── Bootstrap/FeatureCheckerTests.cs
│   ├── Windows/WindowEnumeratorTests.cs
│   ├── Hotkeys/HotkeyManagerTests.cs
│   ├── Audio/AudioMuteServiceTests.cs        ([Trait Category=RequiresDesktop])
│   └── Capture/CaptureServiceTests.cs        ([Trait Category=RequiresDesktop])
├── tools/
│   ├── smoke-test.ps1
│   └── visual-verify.ps1
├── .github/workflows/ci.yml
└── docs/
    └── superpowers/
        ├── specs/2026-05-27-mini-preview-design.md
        └── plans/2026-05-27-mini-preview.md    (this file)
```

---

## Task 1: Scaffolding — solution, project, gitignore, Directory.Build.props

**Files:**
- Create: `MiniPreview.sln`
- Create: `src/MiniPreview/MiniPreview.csproj`
- Create: `src/MiniPreview/App.xaml`
- Create: `src/MiniPreview/App.xaml.cs`
- Create: `tests/MiniPreview.Tests/MiniPreview.Tests.csproj`
- Create: `tests/MiniPreview.Tests/SmokeTest.cs`
- Create: `.gitignore`
- Create: `Directory.Build.props`

- [ ] **Step 1: Create .gitignore**

Write to `.gitignore`:

```gitignore
# Build artefakty
bin/
obj/
out/
publish/
*.user
*.suo
.vs/
.vscode/

# IDE
*.swp
.idea/
*.DotSettings.user

# OS
Thumbs.db
desktop.ini
.DS_Store

# Local
*.local.json
*.local.ps1
tools/screenshots/

# Logy
*.log
```

- [ ] **Step 2: Create Directory.Build.props**

Write to `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsNotAsErrors>CS1591</WarningsNotAsErrors>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Create main app csproj**

Write to `src/MiniPreview/MiniPreview.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <RootNamespace>MiniPreview</RootNamespace>
    <AssemblyName>MiniPreview</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
</Project>
```

Write to `src/MiniPreview/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="MiniPreview"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="asInvoker" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

Write to `src/MiniPreview/App.xaml`:

```xml
<Application x:Class="MiniPreview.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml/2006"
             StartupUri="UI/PreviewWindow.xaml">
    <Application.Resources/>
</Application>
```

Write to `src/MiniPreview/App.xaml.cs`:

```csharp
using System.Windows;

namespace MiniPreview;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }
}
```

Vytvoř prázdný placeholder `src/MiniPreview/UI/PreviewWindow.xaml`:

```xml
<Window x:Class="MiniPreview.UI.PreviewWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml/2006"
        Title="MiniPreview" Width="320" Height="240">
    <Grid Background="Black"/>
</Window>
```

Write to `src/MiniPreview/UI/PreviewWindow.xaml.cs`:

```csharp
using System.Windows;

namespace MiniPreview.UI;

public partial class PreviewWindow : Window
{
    public PreviewWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 4: Create tests csproj**

Write to `tests/MiniPreview.Tests/MiniPreview.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <RootNamespace>MiniPreview.Tests</RootNamespace>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="NSubstitute" Version="5.1.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\MiniPreview\MiniPreview.csproj" />
  </ItemGroup>
</Project>
```

Write to `tests/MiniPreview.Tests/SmokeTest.cs`:

```csharp
namespace MiniPreview.Tests;

public class SmokeTest
{
    [Fact]
    public void TestRunnerWorks()
    {
        Assert.Equal(4, 2 + 2);
    }
}
```

- [ ] **Step 5: Create solution + add projects**

Run:

```powershell
dotnet new sln -n MiniPreview --force
dotnet sln add src/MiniPreview/MiniPreview.csproj
dotnet sln add tests/MiniPreview.Tests/MiniPreview.Tests.csproj
```

Expected: `MiniPreview.sln` exists, no errors.

- [ ] **Step 6: Build + run smoke test**

Run:

```powershell
dotnet build
dotnet test --filter "FullyQualifiedName~SmokeTest"
```

Expected: Build OK (může být pár warnings o WPF SDK, ale 0 errors). Test `TestRunnerWorks` PASS.

- [ ] **Step 7: Commit**

```powershell
git add .gitignore Directory.Build.props MiniPreview.sln src/MiniPreview/MiniPreview.csproj src/MiniPreview/app.manifest src/MiniPreview/App.xaml src/MiniPreview/App.xaml.cs src/MiniPreview/UI/PreviewWindow.xaml src/MiniPreview/UI/PreviewWindow.xaml.cs tests/MiniPreview.Tests/MiniPreview.Tests.csproj tests/MiniPreview.Tests/SmokeTest.cs
git commit -m "chore: scaffold .NET 8 WPF solution + xunit tests"
```

---

## Task 2: NuGet dependencies

**Files:**
- Modify: `src/MiniPreview/MiniPreview.csproj`

- [ ] **Step 1: Add references for Vortice + NAudio**

Edit `src/MiniPreview/MiniPreview.csproj` — add after the existing `</PropertyGroup>`:

```xml
  <ItemGroup>
    <PackageReference Include="Vortice.Direct3D11" Version="3.5.0" />
    <PackageReference Include="Vortice.DXGI" Version="3.5.0" />
    <PackageReference Include="NAudio" Version="2.2.1" />
  </ItemGroup>
```

`Microsoft.Windows.CsWinRT` projection pro WinRT (Windows.Graphics.Capture, Windows.Media.Devices, atd.) je automaticky zahrnutá v target frameworku `net8.0-windows10.0.19041.0` — žádný extra package není potřeba.

- [ ] **Step 2: Restore + build**

Run:

```powershell
dotnet restore
dotnet build
```

Expected: 0 errors. Pokud Vortice vyhodí warnings (DPI awareness, atd.) jen je ignoruj, ale errors nesmí být.

- [ ] **Step 3: Commit**

```powershell
git add src/MiniPreview/MiniPreview.csproj
git commit -m "chore: add Vortice.Direct3D11 + NAudio dependencies"
```

---

## Task 3: Settings data classes + SettingsStore (TDD)

**Files:**
- Create: `src/MiniPreview/Settings/Settings.cs`
- Create: `src/MiniPreview/Settings/SettingsStore.cs`
- Create: `tests/MiniPreview.Tests/Settings/SettingsStoreTests.cs`

- [ ] **Step 1: Write failing tests**

Write to `tests/MiniPreview.Tests/Settings/SettingsStoreTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test --filter "FullyQualifiedName~SettingsStoreTests"
```

Expected: FAIL (`SettingsStore` and `SettingsRoot` not defined).

- [ ] **Step 3: Implement Settings data classes**

Write to `src/MiniPreview/Settings/Settings.cs`:

```csharp
namespace MiniPreview.Settings;

public sealed class SettingsRoot
{
    public int Version { get; set; } = 1;
    public WindowSettings Window { get; set; } = new();
    public CaptureSettings Capture { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public bool Autostart { get; set; }
}

public sealed class WindowSettings
{
    public int X { get; set; } = 100;
    public int Y { get; set; } = 100;
    public int Width { get; set; } = 320;
    public int Height { get; set; } = 240;
    public bool AlwaysOnTop { get; set; } = true;
}

public sealed class CaptureSettings
{
    public int Fps { get; set; } = 5;
    public string? LastTargetProcessName { get; set; }
    public string? LastTargetWindowTitle { get; set; }
}

public sealed class HotkeySettings
{
    public HotkeyBinding TogglePause { get; set; } = new() { Modifiers = new[] { "Ctrl", "Alt" }, Key = "P" };
    public HotkeyBinding ToggleMute  { get; set; } = new() { Modifiers = new[] { "Ctrl", "Alt" }, Key = "M" };
    public HotkeyBinding OpenPicker  { get; set; } = new() { Modifiers = new[] { "Ctrl", "Alt" }, Key = "L" };
}

public sealed class HotkeyBinding
{
    public string[] Modifiers { get; set; } = Array.Empty<string>();
    public string Key { get; set; } = "";
}
```

- [ ] **Step 4: Implement SettingsStore**

Write to `src/MiniPreview/Settings/SettingsStore.cs`:

```csharp
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
```

- [ ] **Step 5: Run tests to verify pass**

Run:

```powershell
dotnet test --filter "FullyQualifiedName~SettingsStoreTests"
```

Expected: 3 tests PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/MiniPreview/Settings/Settings.cs src/MiniPreview/Settings/SettingsStore.cs tests/MiniPreview.Tests/Settings/SettingsStoreTests.cs
git commit -m "feat(settings): SettingsStore with JSON persistence + corrupt-file recovery"
```

---

## Task 4: AutostartManager (TDD)

**Files:**
- Create: `src/MiniPreview/Settings/AutostartManager.cs`
- Create: `tests/MiniPreview.Tests/Settings/AutostartManagerTests.cs`

AutostartManager píše do `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Pro testovatelnost přijímá custom root path (testy použijou `HKCU\Software\MiniPreviewTests`).

- [ ] **Step 1: Write failing tests**

Write to `tests/MiniPreview.Tests/Settings/AutostartManagerTests.cs`:

```csharp
using MiniPreview.Settings;
using Microsoft.Win32;

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
        try { Registry.CurrentUser.DeleteSubKey(TestSubKey, false); } catch { }
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
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test --filter "FullyQualifiedName~AutostartManagerTests"
```

Expected: FAIL (`AutostartManager` not defined).

- [ ] **Step 3: Implement AutostartManager**

Write to `src/MiniPreview/Settings/AutostartManager.cs`:

```csharp
using Microsoft.Win32;

namespace MiniPreview.Settings;

public sealed class AutostartManager
{
    private readonly string _subKey;
    private readonly string _valueName;
    private readonly string _exePath;

    public AutostartManager() : this(
        subKey: @"Software\Microsoft\Windows\CurrentVersion\Run",
        valueName: "MiniPreview",
        exePath: System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!)
    { }

    public AutostartManager(string subKey, string valueName, string exePath)
    {
        _subKey = subKey;
        _valueName = valueName;
        _exePath = exePath;
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(_subKey);
            return key?.GetValue(_valueName) is string s && s.Length > 0;
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(_subKey, writable: true);
        key.SetValue(_valueName, _exePath, RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_subKey, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run:

```powershell
dotnet test --filter "FullyQualifiedName~AutostartManagerTests"
```

Expected: 4 PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/MiniPreview/Settings/AutostartManager.cs tests/MiniPreview.Tests/Settings/AutostartManagerTests.cs
git commit -m "feat(settings): AutostartManager via HKCU\\...\\Run"
```

---

## Task 5: FeatureChecker + ProbeResult (TDD)

**Files:**
- Create: `src/MiniPreview/Bootstrap/ProbeResult.cs`
- Create: `src/MiniPreview/Bootstrap/FeatureChecker.cs`
- Create: `tests/MiniPreview.Tests/Bootstrap/FeatureCheckerTests.cs`

Probes jsou `Func<ProbeResult>` registrované do checkeru. Real probes (WGC, services) jsou v default constructoru; v testech zaregistruješ vlastní lambdy.

- [ ] **Step 1: Write failing tests**

Write to `tests/MiniPreview.Tests/Bootstrap/FeatureCheckerTests.cs`:

```csharp
using MiniPreview.Bootstrap;

namespace MiniPreview.Tests.Bootstrap;

public class FeatureCheckerTests
{
    [Fact]
    public void RunProbes_ExecutesAllRegisteredProbes()
    {
        var checker = new FeatureChecker();
        checker.AddProbe("A", () => new ProbeResult("A", Ok: true, "ok", null));
        checker.AddProbe("B", () => new ProbeResult("B", Ok: false, "fail", "fix B"));

        var results = checker.RunProbes();

        Assert.Equal(2, results.Length);
        Assert.Equal("A", results[0].Name);
        Assert.True(results[0].Ok);
        Assert.False(results[1].Ok);
        Assert.Equal("fix B", results[1].FixCommand);
    }

    [Fact]
    public void RunProbes_CatchesProbeException_ReportsFailure()
    {
        var checker = new FeatureChecker();
        checker.AddProbe("Boom", () => throw new InvalidOperationException("kaboom"));

        var results = checker.RunProbes();

        Assert.Single(results);
        Assert.False(results[0].Ok);
        Assert.Contains("kaboom", results[0].Detail);
    }

    [Fact]
    public void AllOk_TrueWhenNoFailures()
    {
        var results = new[]
        {
            new ProbeResult("A", true, "ok", null),
            new ProbeResult("B", true, "ok", null)
        };
        Assert.True(FeatureChecker.AllOk(results));
    }

    [Fact]
    public void AllOk_FalseWhenAnyFails()
    {
        var results = new[]
        {
            new ProbeResult("A", true, "ok", null),
            new ProbeResult("B", false, "fail", "fix")
        };
        Assert.False(FeatureChecker.AllOk(results));
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

```powershell
dotnet test --filter "FullyQualifiedName~FeatureCheckerTests"
```

Expected: FAIL — types missing.

- [ ] **Step 3: Implement ProbeResult + FeatureChecker**

Write to `src/MiniPreview/Bootstrap/ProbeResult.cs`:

```csharp
namespace MiniPreview.Bootstrap;

public sealed record ProbeResult(string Name, bool Ok, string Detail, string? FixCommand);
```

Write to `src/MiniPreview/Bootstrap/FeatureChecker.cs`:

```csharp
using System.ServiceProcess;

namespace MiniPreview.Bootstrap;

public sealed class FeatureChecker
{
    private readonly List<(string Name, Func<ProbeResult> Probe)> _probes = new();

    public void AddProbe(string name, Func<ProbeResult> probe)
        => _probes.Add((name, probe));

    public ProbeResult[] RunProbes()
    {
        var results = new ProbeResult[_probes.Count];
        for (int i = 0; i < _probes.Count; i++)
        {
            try
            {
                results[i] = _probes[i].Probe();
            }
            catch (Exception ex)
            {
                results[i] = new ProbeResult(_probes[i].Name, Ok: false, Detail: ex.Message, FixCommand: null);
            }
        }
        return results;
    }

    public static bool AllOk(IEnumerable<ProbeResult> results) => results.All(r => r.Ok);

    public static FeatureChecker CreateDefault()
    {
        var checker = new FeatureChecker();
        checker.AddProbe("WGC", ProbeWgc);
        checker.AddProbe("Audio Service", () => ProbeService("Audiosrv",
            "Set-Service -Name Audiosrv -StartupType Automatic; Start-Service Audiosrv"));
        checker.AddProbe("DWM Service", () => ProbeService("uxsms",
            "Set-Service -Name uxsms -StartupType Automatic; Start-Service uxsms"));
        return checker;
    }

    private static ProbeResult ProbeWgc()
    {
        try
        {
            var ok = Windows.Graphics.Capture.GraphicsCaptureSession.IsSupported();
            return ok
                ? new ProbeResult("WGC", true, "Windows.Graphics.Capture dostupné", null)
                : new ProbeResult("WGC", false, "WGC nepodporováno na tomto systému (vyžaduje Win10 1903+)", null);
        }
        catch (Exception ex)
        {
            return new ProbeResult("WGC", false, ex.Message, null);
        }
    }

    private static ProbeResult ProbeService(string serviceName, string fixCommand)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            var running = sc.Status == ServiceControllerStatus.Running;
            return running
                ? new ProbeResult(serviceName, true, $"Služba {serviceName} běží", null)
                : new ProbeResult(serviceName, false, $"Služba {serviceName} neběží ({sc.Status})", fixCommand);
        }
        catch (Exception ex)
        {
            return new ProbeResult(serviceName, false, ex.Message, fixCommand);
        }
    }
}
```

Pozn: `System.ServiceProcess.ServiceController` — přidat NuGet `System.ServiceProcess.ServiceController` v dalším kroku, jinak nepůjde build.

- [ ] **Step 4: Add ServiceController NuGet**

Edit `src/MiniPreview/MiniPreview.csproj` — přidej do existující `<ItemGroup>` s PackageReferences:

```xml
    <PackageReference Include="System.ServiceProcess.ServiceController" Version="8.0.1" />
```

Run:

```powershell
dotnet restore
dotnet build
```

Expected: 0 errors.

- [ ] **Step 5: Run tests to verify pass**

```powershell
dotnet test --filter "FullyQualifiedName~FeatureCheckerTests"
```

Expected: 4 PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/MiniPreview/Bootstrap/ProbeResult.cs src/MiniPreview/Bootstrap/FeatureChecker.cs src/MiniPreview/MiniPreview.csproj tests/MiniPreview.Tests/Bootstrap/FeatureCheckerTests.cs
git commit -m "feat(bootstrap): FeatureChecker with WGC + service probes"
```

---

## Task 6: NativeMethods.cs — User32/Shell32 P/Invoke

**Files:**
- Create: `src/MiniPreview/Windows/NativeMethods.cs`

Pure P/Invoke deklarace. Nemá smysl unit-testovat (jen deklarace); testují se nepřímo skrz WindowEnumerator/WindowPicker.

- [ ] **Step 1: Write NativeMethods**

Write to `src/MiniPreview/Windows/NativeMethods.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Text;

namespace MiniPreview.Windows;

internal static class NativeMethods
{
    // ---------- User32 ----------
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT pt);

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT pt);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    // ---------- Kernel32 ----------
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    // ---------- Shell32 (icons) ----------
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    // ---------- Struct + consts ----------
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // GetWindow / GetAncestor flags
    public const uint GA_ROOT = 2;
    public const uint GW_OWNER = 4;

    // GetWindowLong
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x80;
    public const int WS_EX_APPWINDOW = 0x40000;

    // SHGetFileInfo
    public const uint SHGFI_ICON = 0x100;
    public const uint SHGFI_SMALLICON = 0x1;
    public const uint SHGFI_LARGEICON = 0x0;

    // Hook
    public const int WH_MOUSE_LL = 14;
    public const int WM_LBUTTONDOWN = 0x0201;

    // Hotkey modifiers
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int WM_HOTKEY = 0x0312;
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```powershell
git add src/MiniPreview/Windows/NativeMethods.cs
git commit -m "chore: P/Invoke declarations for User32/Shell32/Kernel32"
```

---

## Task 7: WindowInfo + WindowEnumerator (TDD)

**Files:**
- Create: `src/MiniPreview/Windows/WindowInfo.cs`
- Create: `src/MiniPreview/Windows/WindowEnumerator.cs`
- Create: `tests/MiniPreview.Tests/Windows/WindowEnumeratorTests.cs`

Enumerátor zavolá `EnumWindows`. Pro testovatelnost má injektovatelné `IWindowProvider` rozhraní (real impl volá Win32, mock vrací list). Tím testujeme filtraci (skip empty title, skip invisible, skip tool windows) bez závislosti na desktopu.

- [ ] **Step 1: Write failing tests**

Write to `tests/MiniPreview.Tests/Windows/WindowEnumeratorTests.cs`:

```csharp
using MiniPreview.Windows;
using NSubstitute;

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
```

- [ ] **Step 2: Run tests to verify failure**

```powershell
dotnet test --filter "FullyQualifiedName~WindowEnumeratorTests"
```

Expected: FAIL.

- [ ] **Step 3: Implement WindowInfo + IWindowProvider + Win32WindowProvider + WindowEnumerator**

Write to `src/MiniPreview/Windows/WindowInfo.cs`:

```csharp
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
```

Write to `src/MiniPreview/Windows/WindowEnumerator.cs`:

```csharp
using System.Diagnostics;
using System.Text;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using static MiniPreview.Windows.NativeMethods;

namespace MiniPreview.Windows;

public interface IWindowProvider
{
    IEnumerable<IntPtr> GetTopLevelHandles();
    bool IsVisible(IntPtr hwnd);
    string GetTitle(IntPtr hwnd);
    uint GetProcessId(IntPtr hwnd);
    string GetProcessName(uint pid);
    bool IsToolWindow(IntPtr hwnd);
    string? GetProcessExePath(uint pid);
}

public sealed class Win32WindowProvider : IWindowProvider
{
    public IEnumerable<IntPtr> GetTopLevelHandles()
    {
        var list = new List<IntPtr>();
        EnumWindows((h, _) => { list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }

    public bool IsVisible(IntPtr hwnd) => IsWindowVisible(hwnd);

    public string GetTitle(IntPtr hwnd)
    {
        var len = GetWindowTextLength(hwnd);
        if (len <= 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public uint GetProcessId(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    public string GetProcessName(uint pid)
    {
        try { return Process.GetProcessById((int)pid).ProcessName; }
        catch { return "(unknown)"; }
    }

    public bool IsToolWindow(IntPtr hwnd)
    {
        var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        return (ex & WS_EX_TOOLWINDOW) != 0 && (ex & WS_EX_APPWINDOW) == 0;
    }

    public string? GetProcessExePath(uint pid)
    {
        try { return Process.GetProcessById((int)pid).MainModule?.FileName; }
        catch { return null; }
    }
}

public sealed class WindowEnumerator
{
    private readonly IWindowProvider _provider;

    public WindowEnumerator() : this(new Win32WindowProvider()) { }
    public WindowEnumerator(IWindowProvider provider) => _provider = provider;

    public IEnumerable<WindowInfo> EnumerateVisibleWindows()
    {
        foreach (var hwnd in _provider.GetTopLevelHandles())
        {
            if (!_provider.IsVisible(hwnd)) continue;
            var title = _provider.GetTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title)) continue;
            if (_provider.IsToolWindow(hwnd)) continue;

            var pid = _provider.GetProcessId(hwnd);
            var procName = _provider.GetProcessName(pid);
            var icon = TryLoadIcon(_provider.GetProcessExePath(pid));

            yield return new WindowInfo
            {
                Handle = hwnd,
                Title = title,
                ProcessId = pid,
                ProcessName = procName,
                Icon = icon
            };
        }
    }

    private static System.Windows.Media.ImageSource? TryLoadIcon(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return null;
        var info = new SHFILEINFO();
        var size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(info);
        var ptr = SHGetFileInfo(exePath, 0, ref info, size, SHGFI_ICON | SHGFI_SMALLICON);
        if (ptr == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;

        try
        {
            var bmp = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bmp.Freeze();
            return bmp;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

```powershell
dotnet test --filter "FullyQualifiedName~WindowEnumeratorTests"
```

Expected: 3 PASS.

- [ ] **Step 5: Manual smoke — vypsat reálná okna**

Quick sanity check že real enumerace funguje. Vytvoř dočasný script `src/MiniPreview/Program.Manual.cs`:

```csharp
// docasny manual smoke, smazat po overeni
namespace MiniPreview;
internal static class ManualSmoke
{
    public static void Run()
    {
        var en = new Windows.WindowEnumerator();
        foreach (var w in en.EnumerateVisibleWindows())
            System.Console.WriteLine($"{w.Handle:X8} [{w.ProcessName}] {w.Title}");
    }
}
```

NEZAKLÁDEJ to commitem; v dalším taskem se to bude přesouvat. Pro teď ho zkus zavolat manuálně v testech přidáním:

Write to `tests/MiniPreview.Tests/Windows/WindowEnumeratorManualTests.cs`:

```csharp
using MiniPreview.Windows;
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
```

Smaž `Program.Manual.cs` pokud byl vytvořen.

Run:

```powershell
dotnet test --filter "Category=RequiresDesktop&FullyQualifiedName~WindowEnumeratorManualTests"
```

Expected: PASS s vypsanou listou (Visual Studio, prohlížeč, etc.). Pokud Empty, někde je bug ve filtraci.

- [ ] **Step 6: Commit**

```powershell
git add src/MiniPreview/Windows/WindowInfo.cs src/MiniPreview/Windows/WindowEnumerator.cs tests/MiniPreview.Tests/Windows/WindowEnumeratorTests.cs tests/MiniPreview.Tests/Windows/WindowEnumeratorManualTests.cs
git commit -m "feat(windows): WindowEnumerator with filtering + Win32 provider"
```

---

## Task 8: WindowPicker (click-to-pick via mouse hook)

**Files:**
- Create: `src/MiniPreview/Windows/WindowPicker.cs`

WindowPicker není dobře unit-testovatelný (mouse hook je global state). Stačí dobře navržené API a manuální verifikace v Task 16 (Preview UI). Nepíšeme automated test — místo toho zdokumentujeme jak ručně otestovat.

- [ ] **Step 1: Implement WindowPicker**

Write to `src/MiniPreview/Windows/WindowPicker.cs`:

```csharp
using System.Runtime.InteropServices;
using static MiniPreview.Windows.NativeMethods;

namespace MiniPreview.Windows;

public sealed class WindowPicker : IDisposable
{
    private LowLevelMouseProc? _proc;
    private IntPtr _hook = IntPtr.Zero;
    private Action<IntPtr>? _onPicked;
    private bool _disposed;

    public void BeginPick(Action<IntPtr> onPicked)
    {
        if (_hook != IntPtr.Zero) Cancel();
        _onPicked = onPicked;
        _proc = HookCallback;
        var hModule = GetModuleHandle(null);
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, hModule, 0);
        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException("Nepodařilo se zaregistrovat low-level mouse hook (chyba: " + Marshal.GetLastWin32Error() + ")");
    }

    public void Cancel()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _onPicked = null;
        _proc = null;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var hwnd = WindowFromPoint(data.pt);
            // top-level (project potomky na rodiče)
            var root = GetAncestor(hwnd, GA_ROOT);
            var picked = _onPicked;
            Cancel();
            picked?.Invoke(root);
            return (IntPtr)1; // swallow the click
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Cancel();
        _disposed = true;
    }
}
```

- [ ] **Step 2: Build + smoke**

```powershell
dotnet build
```

Expected: 0 errors. (Real verifikace v Task 16 přes "Pick window" tlačítko v PreviewWindow.)

- [ ] **Step 3: Commit**

```powershell
git add src/MiniPreview/Windows/WindowPicker.cs
git commit -m "feat(windows): WindowPicker with LL mouse hook click-to-pick"
```

---

## Task 9: HotkeyManager (TDD pro pure logic, integration pro Win32 wrappers)

**Files:**
- Create: `src/MiniPreview/Hotkeys/HotkeyDefinition.cs`
- Create: `src/MiniPreview/Hotkeys/HotkeyManager.cs`
- Create: `tests/MiniPreview.Tests/Hotkeys/HotkeyManagerTests.cs`

HotkeyDefinition parsuje stringy z settings ("Ctrl", "Alt", "P") na Win32 flags + VK code. To je čistá logika → TDD.

- [ ] **Step 1: Write failing tests pro definition parsing**

Write to `tests/MiniPreview.Tests/Hotkeys/HotkeyManagerTests.cs`:

```csharp
using MiniPreview.Hotkeys;

namespace MiniPreview.Tests.Hotkeys;

public class HotkeyDefinitionTests
{
    [Fact]
    public void Parse_CtrlAltP_ReturnsCorrectFlags()
    {
        var def = HotkeyDefinition.Parse(new[] { "Ctrl", "Alt" }, "P");
        Assert.Equal(0x0001u | 0x0002u, def.Modifiers); // MOD_ALT | MOD_CONTROL
        Assert.Equal(0x50u, def.VirtualKey); // VK_P
    }

    [Fact]
    public void Parse_LowercaseModifiers_Works()
    {
        var def = HotkeyDefinition.Parse(new[] { "ctrl", "shift", "win" }, "f1");
        Assert.Equal(0x0002u | 0x0004u | 0x0008u, def.Modifiers);
        Assert.Equal(0x70u, def.VirtualKey); // VK_F1
    }

    [Fact]
    public void Parse_UnknownModifier_Throws()
    {
        Assert.Throws<ArgumentException>(() => HotkeyDefinition.Parse(new[] { "Meta" }, "P"));
    }

    [Fact]
    public void Parse_UnknownKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => HotkeyDefinition.Parse(new[] { "Ctrl" }, "GibberishKey"));
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

```powershell
dotnet test --filter "FullyQualifiedName~HotkeyDefinitionTests"
```

Expected: FAIL.

- [ ] **Step 3: Implement HotkeyDefinition + HotkeyManager**

Write to `src/MiniPreview/Hotkeys/HotkeyDefinition.cs`:

```csharp
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
```

Write to `src/MiniPreview/Hotkeys/HotkeyManager.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Windows.Interop;
using static MiniPreview.Windows.NativeMethods;

namespace MiniPreview.Hotkeys;

public sealed class HotkeyManager : IDisposable
{
    private readonly Dictionary<int, Action> _actions = new();
    private readonly HwndSource _source;
    private int _nextId = 1;
    private bool _disposed;

    public HotkeyManager()
    {
        // message-only HWND (HWND_MESSAGE jako parent = -3)
        var parameters = new HwndSourceParameters("MiniPreviewHotkeyWindow")
        {
            ParentWindow = new IntPtr(-3),
            HwndSourceHook = WndProc
        };
        _source = new HwndSource(parameters);
    }

    public int Register(HotkeyDefinition def, Action action)
    {
        var id = _nextId++;
        var modsNoRepeat = def.Modifiers | MOD_NOREPEAT;
        if (!RegisterHotKey(_source.Handle, id, modsNoRepeat, def.VirtualKey))
            throw new InvalidOperationException($"RegisterHotKey selhal (mods=0x{def.Modifiers:X}, vk=0x{def.VirtualKey:X}). Win32 error: {Marshal.GetLastWin32Error()}");
        _actions[id] = action;
        return id;
    }

    public void Unregister(int id)
    {
        if (_actions.Remove(id))
            UnregisterHotKey(_source.Handle, id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys.ToList())
            Unregister(id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_actions.TryGetValue(id, out var action))
            {
                action();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        UnregisterAll();
        _source.Dispose();
        _disposed = true;
    }
}
```

- [ ] **Step 4: Add integration test pro real Register/Unregister (RequiresDesktop)**

Add to `tests/MiniPreview.Tests/Hotkeys/HotkeyManagerTests.cs` (append na konec souboru):

```csharp
[Trait("Category", "RequiresDesktop")]
public class HotkeyManagerIntegrationTests
{
    [Fact]
    public void Register_AndUnregister_UnusedCombo_Succeeds()
    {
        // Pouzij neobvyklou kombinaci aby nebyla kolize: Ctrl+Alt+Shift+F24
        var def = HotkeyDefinition.Parse(new[] { "Ctrl", "Alt", "Shift" }, "F24");

        // WPF HwndSource musi byt vytvoreny na STA threadu
        Exception? caught = null;
        var t = new Thread(() =>
        {
            try
            {
                using var mgr = new HotkeyManager();
                var id = mgr.Register(def, () => { });
                Assert.True(id > 0);
                mgr.Unregister(id);
            }
            catch (Exception ex) { caught = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (caught != null) throw caught;
    }
}
```

- [ ] **Step 5: Run all hotkey tests**

```powershell
dotnet test --filter "FullyQualifiedName~Hotkey"
```

Expected: 4 unit tests PASS + 1 integration PASS (5 total).

- [ ] **Step 6: Commit**

```powershell
git add src/MiniPreview/Hotkeys/HotkeyDefinition.cs src/MiniPreview/Hotkeys/HotkeyManager.cs tests/MiniPreview.Tests/Hotkeys/HotkeyManagerTests.cs
git commit -m "feat(hotkeys): HotkeyManager with message-only HWND + parsing"
```

---

## Task 10: AudioMuteService (integration test against real audio session)

**Files:**
- Create: `src/MiniPreview/Audio/AudioMuteService.cs`
- Create: `tests/MiniPreview.Tests/Audio/AudioMuteServiceTests.cs`

NAudio API. Per-PID mute = enumerate sessions → match `GetProcessID()` → set `SimpleAudioVolume.Mute`. Real test spustí `winmm` test tone (vlastní proces — `dotnet test` worker) a ověří mute toggle.

- [ ] **Step 1: Implement AudioMuteService**

Write to `src/MiniPreview/Audio/AudioMuteService.cs`:

```csharp
using NAudio.CoreAudioApi;

namespace MiniPreview.Audio;

public sealed class AudioMuteService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private bool _disposed;

    public AudioMuteService()
    {
        _enumerator = new MMDeviceEnumerator();
    }

    /// <summary>Toggle mute state pro všechny sessiony patřící danému PID. Vrátí nový state (true = muted).</summary>
    public bool ToggleMute(uint pid)
    {
        var sessions = FindSessions(pid);
        if (sessions.Count == 0) return false;

        var newState = !sessions[0].SimpleAudioVolume.Mute;
        foreach (var s in sessions)
            s.SimpleAudioVolume.Mute = newState;
        return newState;
    }

    public bool IsMuted(uint pid)
    {
        var sessions = FindSessions(pid);
        return sessions.Count > 0 && sessions[0].SimpleAudioVolume.Mute;
    }

    public bool HasAudioSession(uint pid) => FindSessions(pid).Count > 0;

    private List<AudioSessionControl> FindSessions(uint pid)
    {
        var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sessions = device.AudioSessionManager.Sessions;
        var matches = new List<AudioSessionControl>();
        for (int i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            if (s.GetProcessID == pid)
                matches.Add(s);
        }
        return matches;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _enumerator.Dispose();
        _disposed = true;
    }
}
```

- [ ] **Step 2: Write integration test**

Write to `tests/MiniPreview.Tests/Audio/AudioMuteServiceTests.cs`:

```csharp
using MiniPreview.Audio;
using NAudio.Wave;

namespace MiniPreview.Tests.Audio;

[Trait("Category", "RequiresDesktop")]
public class AudioMuteServiceTests
{
    [Fact]
    public void ToggleMute_FlipsState_ForCurrentProcess()
    {
        // Hraj kratky silent tone aby se zaregistroval audio session
        using var waveOut = new WaveOutEvent();
        using var generator = new SilenceProvider(new WaveFormat(44100, 1));
        waveOut.Init(generator);
        waveOut.Play();
        // chvili pockej nez se session zaregistruje
        Thread.Sleep(500);

        var pid = (uint)Environment.ProcessId;
        using var mute = new AudioMuteService();

        if (!mute.HasAudioSession(pid))
        {
            // session se nezaregistroval -> skip s vysvetlenim
            Skip.If(true, "Audio session nebyl detekovan pro testovaci proces (mozna headless prostredi)");
            return;
        }

        var initial = mute.IsMuted(pid);
        var toggled = mute.ToggleMute(pid);
        Assert.NotEqual(initial, toggled);
        var toggledBack = mute.ToggleMute(pid);
        Assert.Equal(initial, toggledBack);

        waveOut.Stop();
    }
}

internal sealed class SilenceProvider : IWaveProvider
{
    public SilenceProvider(WaveFormat fmt) { WaveFormat = fmt; }
    public WaveFormat WaveFormat { get; }
    public int Read(byte[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        return count;
    }
}

// Mini polyfill na Skip.If aby nebyl extra dep:
internal static class Skip
{
    public static void If(bool cond, string reason)
    {
        if (cond) throw new SkipException(reason);
    }
}
internal sealed class SkipException : Exception { public SkipException(string m) : base(m) { } }
```

Pozn: `xunit` neumí "skip" nativně — `SkipException` jen vyhodí výjimku, test bude FAIL místo SKIPPED. To je OK pro nás (značí to "neumím otestovat v tomto prostředí"). Pokud chceš real skip, přidej `Xunit.SkippableFact` NuGet — tady to neděláme abychom drželi deps minimální.

- [ ] **Step 3: Build + test**

```powershell
dotnet build
dotnet test --filter "FullyQualifiedName~AudioMuteServiceTests"
```

Expected: PASS (na desktopu, kde funguje audio device). Pokud headless → SkipException jako FAIL — to je očekávané.

- [ ] **Step 4: Commit**

```powershell
git add src/MiniPreview/Audio/AudioMuteService.cs tests/MiniPreview.Tests/Audio/AudioMuteServiceTests.cs
git commit -m "feat(audio): AudioMuteService per-PID mute via NAudio"
```

---

## Task 11: D3D11Helpers (Vortice device + staging texture)

**Files:**
- Create: `src/MiniPreview/Capture/D3D11Helpers.cs`

Wrapper nad Vortice — vytvoří D3D11 device + immediate context, kopíruje GPU texturu do staging textury a mapuje ji pro CPU read. Test bude jako součást CaptureService integrace; tady jen kód.

- [ ] **Step 1: Implement D3D11Helpers**

Write to `src/MiniPreview/Capture/D3D11Helpers.cs`:

```csharp
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace MiniPreview.Capture;

internal sealed class D3D11Device : IDisposable
{
    public ID3D11Device Device { get; }
    public ID3D11DeviceContext Context { get; }

    public D3D11Device()
    {
        var flags = DeviceCreationFlags.BgraSupport;
        var levels = new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0 };
        var result = D3D11.D3D11CreateDevice(
            adapter: null,
            DriverType.Hardware,
            flags,
            levels,
            out var device,
            out var context);
        if (result.Failure) throw new InvalidOperationException("D3D11CreateDevice selhal: " + result.Description);
        Device = device!;
        Context = context!;
    }

    public void Dispose()
    {
        Context?.Dispose();
        Device?.Dispose();
    }
}

internal sealed class StagingTexturePool : IDisposable
{
    private readonly ID3D11Device _device;
    private ID3D11Texture2D? _staging;
    private int _width, _height;

    public StagingTexturePool(ID3D11Device device) { _device = device; }

    public ID3D11Texture2D Get(int width, int height)
    {
        if (_staging != null && _width == width && _height == height) return _staging;

        _staging?.Dispose();
        var desc = new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None
        };
        _staging = _device.CreateTexture2D(desc);
        _width = width;
        _height = height;
        return _staging;
    }

    public void Dispose() { _staging?.Dispose(); _staging = null; }
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```powershell
git add src/MiniPreview/Capture/D3D11Helpers.cs
git commit -m "feat(capture): D3D11 device + staging texture pool via Vortice"
```

---

## Task 12: IGraphicsCaptureItemInterop (COM interface)

**Files:**
- Create: `src/MiniPreview/Capture/IGraphicsCaptureItemInterop.cs`

WGC nemá public API pro vytvoření `GraphicsCaptureItem` z HWND v managed kódu — musí se to udělat přes interop COM rozhraní. Toto je stadard pattern používaný i v Microsoftím samplu.

- [ ] **Step 1: Implement interop interface**

Write to `src/MiniPreview/Capture/IGraphicsCaptureItemInterop.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using WinRT;
using Windows.Graphics.Capture;

namespace MiniPreview.Capture;

[ComImport]
[Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IGraphicsCaptureItemInterop
{
    IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
    IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
}

internal static class CaptureItemFactory
{
    private static readonly Guid IID_IGraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    public static GraphicsCaptureItem CreateForWindow(IntPtr hwnd)
    {
        var factory = WinRT.ActivationFactory.Get<IGraphicsCaptureItemInterop>("Windows.Graphics.Capture.GraphicsCaptureItem");
        var iid = IID_IGraphicsCaptureItem;
        var rawPtr = factory.CreateForWindow(hwnd, ref iid);
        return MarshalInterface<GraphicsCaptureItem>.FromAbi(rawPtr);
    }

    public static GraphicsCaptureItem CreateForMonitor(IntPtr hmonitor)
    {
        var factory = WinRT.ActivationFactory.Get<IGraphicsCaptureItemInterop>("Windows.Graphics.Capture.GraphicsCaptureItem");
        var iid = IID_IGraphicsCaptureItem;
        var rawPtr = factory.CreateForMonitor(hmonitor, ref iid);
        return MarshalInterface<GraphicsCaptureItem>.FromAbi(rawPtr);
    }
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build
```

Expected: 0 errors. Pokud `WinRT.ActivationFactory` nebo `MarshalInterface` nejsou nalezené, přidej do csproj:

```xml
    <PackageReference Include="Microsoft.Windows.CsWinRT" Version="2.1.5" />
```

Run `dotnet restore` a `dotnet build` znovu.

- [ ] **Step 3: Commit**

```powershell
git add src/MiniPreview/Capture/IGraphicsCaptureItemInterop.cs src/MiniPreview/MiniPreview.csproj
git commit -m "feat(capture): IGraphicsCaptureItemInterop + factory for window/monitor"
```

---

## Task 13: CaptureService (WGC lifecycle + pacing + FrameReady)

**Files:**
- Create: `src/MiniPreview/Capture/CaptureService.cs`
- Create: `tests/MiniPreview.Tests/Capture/CaptureServiceTests.cs`

Core piece. Spousta P/Invoke + WinRT. TDD pouze pro pacing logiku (FPS gate); WGC samotné testujeme integračně proti reálnému Notepadu.

- [ ] **Step 1: Implement CaptureService**

Write to `src/MiniPreview/Capture/CaptureService.cs`:

```csharp
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace MiniPreview.Capture;

/// <summary>One captured frame: raw BGRA pixel data + dimensions. Consumer (UI) converts na bitmapu.</summary>
public sealed record CapturedFrame(byte[] Bgra, int Width, int Height, int SourceStride);

public sealed class CaptureService : IDisposable
{
    private D3D11Device? _d3d;
    private StagingTexturePool? _pool;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private IDirect3DDevice? _winrtDevice;
    private int _fps = 5;
    private long _lastFrameTicks;
    private bool _paused;
    private readonly object _gate = new();

    /// <summary>Fired from a worker thread. Consumer must marshal to UI thread.</summary>
    public event Action<CapturedFrame>? FrameReady;
    public event Action? TargetClosed;

    public bool IsRunning => _session != null;
    public bool IsPaused => _paused;
    public int Fps => _fps;

    public void Start(IntPtr hwnd, int fps)
    {
        Stop();
        lock (_gate)
        {
            _fps = Math.Clamp(fps, 1, 60);
            _d3d = new D3D11Device();
            _pool = new StagingTexturePool(_d3d.Device);
            _winrtDevice = CreateWinRTDeviceFromD3D11(_d3d.Device);
            _item = CaptureItemFactory.CreateForWindow(hwnd);
            _item.Closed += OnItemClosed;

            var size = _item.Size;
            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _winrtDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                numberOfBuffers: 2,
                size);
            _framePool.FrameArrived += OnFrameArrived;

            _session = _framePool.CreateCaptureSession(_item);
            _session.IsCursorCaptureEnabled = false;
            _session.StartCapture();
            _lastFrameTicks = 0;
            _paused = false;
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            _paused = true;
            // Tear down session pro 0% overhead
            _session?.Dispose(); _session = null;
            _framePool?.Dispose(); _framePool = null;
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (!_paused || _item == null || _winrtDevice == null) return;
            _paused = false;
            var size = _item.Size;
            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(_winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);
            _framePool.FrameArrived += OnFrameArrived;
            _session = _framePool.CreateCaptureSession(_item);
            _session.IsCursorCaptureEnabled = false;
            _session.StartCapture();
            _lastFrameTicks = 0;
        }
    }

    public void SetFps(int fps)
    {
        lock (_gate) { _fps = Math.Clamp(fps, 1, 60); }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _session?.Dispose(); _session = null;
            _framePool?.Dispose(); _framePool = null;
            if (_item != null) _item.Closed -= OnItemClosed;
            _item = null;
            _pool?.Dispose(); _pool = null;
            _d3d?.Dispose(); _d3d = null;
            _winrtDevice?.As<IDisposable>()?.Dispose();
            _winrtDevice = null;
            _paused = false;
        }
    }

    private void OnItemClosed(GraphicsCaptureItem sender, object args)
    {
        Stop();
        TargetClosed?.Invoke();
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        // Pacing gate
        var now = Environment.TickCount64;
        var minInterval = 1000L / _fps;
        if (_lastFrameTicks != 0 && now - _lastFrameTicks < minInterval)
        {
            using var skip = sender.TryGetNextFrame();
            return;
        }
        _lastFrameTicks = now;

        using var frame = sender.TryGetNextFrame();
        if (frame == null) return;
        ProcessFrame(frame);
    }

    private void ProcessFrame(Direct3D11CaptureFrame frame)
    {
        if (_d3d == null || _pool == null) return;

        // Frame.Surface -> ID3D11Texture2D
        var sourceTexture = D3D11InteropHelpers.GetTexture(frame.Surface);
        var size = frame.ContentSize;
        if (size.Width <= 0 || size.Height <= 0) return;

        var staging = _pool.Get(size.Width, size.Height);
        _d3d.Context.CopyResource(staging, sourceTexture);

        var mapped = _d3d.Context.Map(staging, 0, MapMode.Read);
        try
        {
            int srcStride = (int)mapped.RowPitch;
            var bytes = new byte[srcStride * size.Height];
            Marshal.Copy(mapped.DataPointer, bytes, 0, bytes.Length);
            FrameReady?.Invoke(new CapturedFrame(bytes, size.Width, size.Height, srcStride));
        }
        finally
        {
            _d3d.Context.Unmap(staging, 0);
        }
    }

    private static IDirect3DDevice CreateWinRTDeviceFromD3D11(ID3D11Device d3dDevice)
    {
        using var dxgi = d3dDevice.QueryInterface<IDXGIDevice>();
        Direct3D11InteropFunctions.CreateDirect3D11DeviceFromDXGIDevice(dxgi.NativePointer, out var ptr);
        return MarshalInterface<IDirect3DDevice>.FromAbi(ptr);
    }

    public void Dispose() => Stop();
}

internal static class Direct3D11InteropFunctions
{
    [DllImport("d3d11.dll")]
    public static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
}

internal static class D3D11InteropHelpers
{
    private static readonly Guid IID_ID3D11Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    public static ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        var access = surface.As<IDirect3DDxgiInterfaceAccess>();
        var iid = IID_ID3D11Texture2D;
        var ptr = access.GetInterface(ref iid);
        return new ID3D11Texture2D(ptr);
    }
}

[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDirect3DDxgiInterfaceAccess
{
    IntPtr GetInterface([In] ref Guid iid);
}
```

- [ ] **Step 2: Write integration test (Notepad fixture)**

Write to `tests/MiniPreview.Tests/Capture/CaptureServiceTests.cs`:

```csharp
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using MiniPreview.Capture;
using MiniPreview.Windows;

namespace MiniPreview.Tests.Capture;

[Trait("Category", "RequiresDesktop")]
public class CaptureServiceTests
{
    [StaFact]
    public void Start_AgainstNotepad_DeliversFrameWithin2Seconds()
    {
        if (Application.Current == null) new Application();

        var notepad = Process.Start("notepad.exe");
        try
        {
            // dej notepadu chvili na zobrazeni
            for (int i = 0; i < 20; i++) { if (notepad.MainWindowHandle != IntPtr.Zero) break; Thread.Sleep(100); }
            Assert.NotEqual(IntPtr.Zero, notepad.MainWindowHandle);

            using var capture = new CaptureService();
            var frameSignal = new ManualResetEventSlim();
            capture.FrameReady += _ => frameSignal.Set();

            capture.Start(notepad.MainWindowHandle, fps: 10);

            // pump dispatcher
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (!frameSignal.IsSet && DateTime.UtcNow < deadline)
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Thread.Sleep(10);
            }

            Assert.True(frameSignal.IsSet, "Žádný frame nedoručen do 3 s");
        }
        finally
        {
            try { notepad.Kill(); } catch { }
        }
    }
}

// xUnit nema [StaFact] out of the box -- pridej tridu Skip nebo pouzij Xunit.StaFact NuGet
// Tady jednoduchy polyfill na STA test:
public sealed class StaFactAttribute : FactAttribute { }
```

Pozn: vlastnoručně dělat STA fakt je tricky — pokud test failne s "thread not STA", přidej do test csproj:

```xml
    <PackageReference Include="Xunit.StaFact" Version="1.1.11" />
```

a smaž inline `StaFactAttribute` třídu.

- [ ] **Step 3: Build + test**

```powershell
dotnet restore
dotnet build
dotnet test --filter "FullyQualifiedName~CaptureServiceTests"
```

Expected: PASS (Notepad otevřen, frame přijde do 3 s, test killne Notepad).

Pokud failne na "Application.Current je null po DispatcherInvoke" — to znamená že WPF dispatcher init je neúplný; alternativa: vytvoř `Application` v `[Collection]` fixture a používej `Application.Current.Dispatcher`. Pro účely tohoto testu zkus runner s `xunit.runner.json` configurací `parallelizeTestCollections=false`.

- [ ] **Step 4: Commit**

```powershell
git add src/MiniPreview/Capture/CaptureService.cs tests/MiniPreview.Tests/Capture/CaptureServiceTests.cs tests/MiniPreview.Tests/MiniPreview.Tests.csproj
git commit -m "feat(capture): CaptureService with WGC + pacing + pause/resume"
```

---

## Task 14: App.xaml bootstrap — feature check, settings load, --selftest router

**Files:**
- Modify: `src/MiniPreview/App.xaml`
- Modify: `src/MiniPreview/App.xaml.cs`
- Create: `src/MiniPreview/SelfTest/SelfTestRunner.cs`

App.xaml.cs orchestruje:
1. Parse argumentů — pokud `--selftest`, hodí to `SelfTestRunner` a exitne.
2. Jinak: `FeatureChecker.CreateDefault().RunProbes()`. Pokud něco failne → `FeatureCheckDialog`. Po dialogu buď exit nebo pokračuje.
3. Load settings, hotkeys, vytvoř `PreviewWindow`.

- [ ] **Step 1: Remove StartupUri (budeme spouštět okno ručně)**

Edit `src/MiniPreview/App.xaml` — odstraň `StartupUri="UI/PreviewWindow.xaml"`. Výsledek:

```xml
<Application x:Class="MiniPreview.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml/2006">
    <Application.Resources/>
</Application>
```

- [ ] **Step 2: Implement App.xaml.cs**

Write to `src/MiniPreview/App.xaml.cs`:

```csharp
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

        // Feature check
        var checker = FeatureChecker.CreateDefault();
        var probes = checker.RunProbes();
        if (!FeatureChecker.AllOk(probes))
        {
            var dlg = new FeatureCheckDialog(probes);
            var ok = dlg.ShowDialog();
            if (ok != true) { Shutdown(1); return; }
        }

        // Settings
        SettingsStore = new SettingsStore();
        Settings = SettingsStore.Load();

        // Main window
        var main = new PreviewWindow();
        main.Show();
    }
}
```

- [ ] **Step 3: Implement SelfTestRunner**

Write to `src/MiniPreview/SelfTest/SelfTestRunner.cs`:

```csharp
using System.Diagnostics;
using System.Text.Json;
using MiniPreview.Audio;
using MiniPreview.Bootstrap;
using MiniPreview.Capture;
using MiniPreview.Windows;

namespace MiniPreview.SelfTest;

internal static class SelfTestRunner
{
    public static int Run()
    {
        var probes = FeatureChecker.CreateDefault().RunProbes();
        var probesOk = FeatureChecker.AllOk(probes);

        bool captureOk = false;
        double framesPerSec = 0;
        string? captureError = null;
        try
        {
            // Capture shell window (explorer.exe)
            var hwnd = FindShellWindow();
            if (hwnd == IntPtr.Zero) throw new InvalidOperationException("Nenalezeno shell okno");

            using var capture = new CaptureService();
            int frameCount = 0;
            capture.FrameReady += _ => Interlocked.Increment(ref frameCount);

            var sw = Stopwatch.StartNew();
            capture.Start(hwnd, fps: 10);

            // pump 2s
            while (sw.Elapsed < TimeSpan.FromSeconds(2)) Thread.Sleep(50);
            capture.Stop();

            framesPerSec = frameCount / sw.Elapsed.TotalSeconds;
            captureOk = frameCount > 0;
        }
        catch (Exception ex) { captureError = ex.Message; }

        bool audioOk = false;
        string? audioError = null;
        try
        {
            using var audio = new AudioMuteService();
            // jen test ze instance funguje (nepokousime se mute current proces v selftest)
            audioOk = true;
        }
        catch (Exception ex) { audioError = ex.Message; }

        var report = new
        {
            probes,
            probesOk,
            captureOk,
            captureError,
            framesPerSec,
            audioOk,
            audioError
        };
        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return (probesOk && captureOk && audioOk) ? 0 : 1;
    }

    private static IntPtr FindShellWindow()
    {
        var en = new WindowEnumerator();
        foreach (var w in en.EnumerateVisibleWindows())
        {
            if (w.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                return w.Handle;
        }
        return IntPtr.Zero;
    }
}
```

- [ ] **Step 4: Build**

```powershell
dotnet build
```

Expected: 0 errors. (Pokud FeatureCheckDialog ještě neexistuje, build failne — to je OK, dokončíme v dalším tasku. Pro teď okomentuj/odlož `new FeatureCheckDialog(...)` a `if (!FeatureChecker.AllOk...)` blok celý zastrč do `// TODO: FeatureCheckDialog v task 15` jen DOČASNĚ.)

Aby build prošel teď, dočasně v App.xaml.cs:

```csharp
        if (!FeatureChecker.AllOk(probes))
        {
            // TODO Task 15: ukazat FeatureCheckDialog
            MessageBox.Show("Probe failed:\n" + string.Join("\n", probes.Where(p => !p.Ok).Select(p => $"{p.Name}: {p.Detail}")),
                "MiniPreview", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
```

- [ ] **Step 5: Commit**

```powershell
git add src/MiniPreview/App.xaml src/MiniPreview/App.xaml.cs src/MiniPreview/SelfTest/SelfTestRunner.cs
git commit -m "feat(bootstrap): App startup with --selftest router + feature check"
```

---

## Task 15: FeatureCheckDialog (WPF)

**Files:**
- Create: `src/MiniPreview/UI/FeatureCheckDialog.xaml`
- Create: `src/MiniPreview/UI/FeatureCheckDialog.xaml.cs`
- Modify: `src/MiniPreview/App.xaml.cs` (odstranit TODO MessageBox, nahradit dialogem)

- [ ] **Step 1: Create XAML**

Write to `src/MiniPreview/UI/FeatureCheckDialog.xaml`:

```xml
<Window x:Class="MiniPreview.UI.FeatureCheckDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml/2006"
        Title="MiniPreview — kontrola prostředí"
        Width="640" Height="480"
        WindowStartupLocation="CenterScreen"
        ResizeMode="CanResize">
    <DockPanel Margin="12">
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
            <Button x:Name="RecheckBtn" Content="Recheck" Width="100" Margin="4" Click="OnRecheck"/>
            <Button x:Name="TryAnywayBtn" Content="Try anyway" Width="120" Margin="4" Click="OnTryAnyway"/>
            <Button x:Name="ExitBtn" Content="Exit" Width="100" Margin="4" Click="OnExit" IsCancel="True"/>
        </StackPanel>
        <TextBlock DockPanel.Dock="Top" FontWeight="Bold" FontSize="14" Margin="0,0,0,8">
            Některé komponenty Windows mohou chybět. Detaily:
        </TextBlock>
        <ScrollViewer VerticalScrollBarVisibility="Auto">
            <ItemsControl x:Name="ProbesList"/>
        </ScrollViewer>
    </DockPanel>
</Window>
```

- [ ] **Step 2: Create code-behind**

Write to `src/MiniPreview/UI/FeatureCheckDialog.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MiniPreview.Bootstrap;

namespace MiniPreview.UI;

public partial class FeatureCheckDialog : Window
{
    private ProbeResult[] _probes;

    public FeatureCheckDialog(ProbeResult[] probes)
    {
        InitializeComponent();
        _probes = probes;
        Render();
    }

    private void Render()
    {
        ProbesList.Items.Clear();
        foreach (var p in _probes)
        {
            var border = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 6),
                Background = p.Ok ? Brushes.Honeydew : Brushes.MistyRose
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = $"{(p.Ok ? "✓" : "✗")}  {p.Name}", FontWeight = FontWeights.Bold });
            stack.Children.Add(new TextBlock { Text = p.Detail, TextWrapping = TextWrapping.Wrap });
            if (!p.Ok && !string.IsNullOrEmpty(p.FixCommand))
            {
                var fixPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
                var box = new TextBox
                {
                    Text = p.FixCommand,
                    IsReadOnly = true,
                    FontFamily = new FontFamily("Consolas"),
                    Background = Brushes.White,
                    MinWidth = 400
                };
                var copyBtn = new Button { Content = "Copy", Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2) };
                copyBtn.Click += (_, _) => { Clipboard.SetText(p.FixCommand); copyBtn.Content = "Copied ✓"; };
                fixPanel.Children.Add(box);
                fixPanel.Children.Add(copyBtn);
                stack.Children.Add(fixPanel);
            }
            border.Child = stack;
            ProbesList.Items.Add(border);
        }

        var anyFatal = !_probes.First(p => p.Name == "WGC").Ok;
        TryAnywayBtn.IsEnabled = !anyFatal;
    }

    private void OnRecheck(object sender, RoutedEventArgs e)
    {
        _probes = FeatureChecker.CreateDefault().RunProbes();
        if (FeatureChecker.AllOk(_probes)) { DialogResult = true; Close(); return; }
        Render();
    }

    private void OnTryAnyway(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void OnExit(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
```

- [ ] **Step 3: Wire dialog into App.xaml.cs (odstraň TODO MessageBox)**

Edit `src/MiniPreview/App.xaml.cs` — nahraď celý feature-check blok:

```csharp
        if (!FeatureChecker.AllOk(probes))
        {
            var dlg = new FeatureCheckDialog(probes);
            var ok = dlg.ShowDialog();
            if (ok != true) { Shutdown(1); return; }
        }
```

- [ ] **Step 4: Build**

```powershell
dotnet build
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add src/MiniPreview/UI/FeatureCheckDialog.xaml src/MiniPreview/UI/FeatureCheckDialog.xaml.cs src/MiniPreview/App.xaml.cs
git commit -m "feat(ui): FeatureCheckDialog with probe results + copy-fix buttons"
```

---

## Task 16: PreviewWindow — full UI (drag, resize, context menu, capture wiring)

**Files:**
- Modify: `src/MiniPreview/UI/PreviewWindow.xaml`
- Modify: `src/MiniPreview/UI/PreviewWindow.xaml.cs`

Toto je největší UI task. Borderless top-most okno s `Image` controlem, drag celého okna, resize handle vpravo dole, kontextové menu s vším.

- [ ] **Step 1: Replace XAML**

Write to `src/MiniPreview/UI/PreviewWindow.xaml`:

```xml
<Window x:Class="MiniPreview.UI.PreviewWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml/2006"
        Title="MiniPreview"
        Width="320" Height="240"
        MinWidth="160" MinHeight="120"
        WindowStyle="None" AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        ResizeMode="CanResizeWithGrip">
    <Border BorderBrush="#80FFFFFF" BorderThickness="1" Background="Black" CornerRadius="4">
        <Grid>
            <Image x:Name="PreviewImage" Stretch="Uniform"/>
            <Grid x:Name="StatusOverlay" Visibility="Collapsed" Background="#80000000">
                <TextBlock x:Name="StatusText" Foreground="White" FontSize="16" HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Grid>
            <Grid.ContextMenu>
                <ContextMenu x:Name="MainMenu" Opened="OnMenuOpened">
                    <MenuItem x:Name="WindowsMenu" Header="Vyber okno"/>
                    <MenuItem Header="Pick window..." Click="OnPickWindow"/>
                    <Separator/>
                    <MenuItem x:Name="FpsMenu" Header="FPS">
                        <MenuItem Header="1"  Tag="1"  Click="OnFpsClick"/>
                        <MenuItem Header="2"  Tag="2"  Click="OnFpsClick"/>
                        <MenuItem Header="5"  Tag="5"  Click="OnFpsClick"/>
                        <MenuItem Header="10" Tag="10" Click="OnFpsClick"/>
                        <MenuItem Header="15" Tag="15" Click="OnFpsClick"/>
                        <MenuItem Header="30" Tag="30" Click="OnFpsClick"/>
                    </MenuItem>
                    <MenuItem x:Name="PauseItem" Header="Pauza" IsCheckable="True" Click="OnPauseClick"/>
                    <MenuItem x:Name="MuteItem" Header="Mute target" IsCheckable="True" Click="OnMuteClick"/>
                    <Separator/>
                    <MenuItem Header="Nastavení..." Click="OnSettingsClick"/>
                    <MenuItem Header="Konec" Click="OnExitClick"/>
                </ContextMenu>
            </Grid.ContextMenu>
        </Grid>
    </Border>
</Window>
```

- [ ] **Step 2: Implement code-behind**

Write to `src/MiniPreview/UI/PreviewWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MiniPreview.Audio;
using MiniPreview.Capture;
using MiniPreview.Hotkeys;
using MiniPreview.Settings;
using MiniPreview.Windows;

namespace MiniPreview.UI;

public partial class PreviewWindow : Window
{
    private readonly CaptureService _capture = new();
    private readonly AudioMuteService _audio = new();
    private readonly WindowEnumerator _enumerator = new();
    private WindowPicker? _picker;
    private HotkeyManager? _hotkeys;
    private SettingsStore _store = null!;
    private SettingsRoot _settings = null!;
    private WindowInfo? _currentTarget;

    public PreviewWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        _capture.FrameReady += OnFrameReady;
        _capture.TargetClosed += () => Dispatcher.BeginInvoke(() => ShowStatus("Target lost — vyber okno"));
    }

    private void OnFrameReady(CapturedFrame frame)
    {
        // CaptureService fires from worker thread; convert + assign na UI threadu
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            var wb = new WriteableBitmap(frame.Width, frame.Height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
            wb.Lock();
            try
            {
                int dstStride = frame.Width * 4;
                if (frame.SourceStride == dstStride)
                {
                    System.Runtime.InteropServices.Marshal.Copy(frame.Bgra, 0, wb.BackBuffer, frame.Bgra.Length);
                }
                else
                {
                    for (int y = 0; y < frame.Height; y++)
                        System.Runtime.InteropServices.Marshal.Copy(frame.Bgra, y * frame.SourceStride, wb.BackBuffer + y * dstStride, dstStride);
                }
                wb.AddDirtyRect(new System.Windows.Int32Rect(0, 0, frame.Width, frame.Height));
            }
            finally { wb.Unlock(); }
            wb.Freeze();
            PreviewImage.Source = wb;
        });
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        _store = app.SettingsStore;
        _settings = app.Settings;

        Left = _settings.Window.X;
        Top = _settings.Window.Y;
        Width = _settings.Window.Width;
        Height = _settings.Window.Height;
        Topmost = _settings.Window.AlwaysOnTop;

        InitHotkeys();
        TryResumeLastTarget();
    }

    private void InitHotkeys()
    {
        _hotkeys = new HotkeyManager();
        try
        {
            var pauseDef = HotkeyDefinition.Parse(_settings.Hotkeys.TogglePause.Modifiers, _settings.Hotkeys.TogglePause.Key);
            _hotkeys.Register(pauseDef, () => Dispatcher.BeginInvoke(TogglePause));
            var muteDef = HotkeyDefinition.Parse(_settings.Hotkeys.ToggleMute.Modifiers, _settings.Hotkeys.ToggleMute.Key);
            _hotkeys.Register(muteDef, () => Dispatcher.BeginInvoke(ToggleMute));
            var pickDef = HotkeyDefinition.Parse(_settings.Hotkeys.OpenPicker.Modifiers, _settings.Hotkeys.OpenPicker.Key);
            _hotkeys.Register(pickDef, () => Dispatcher.BeginInvoke(BeginPickWindow));
        }
        catch (Exception ex)
        {
            ShowStatus("Hotkey selhal: " + ex.Message);
        }
    }

    private void TryResumeLastTarget()
    {
        var last = _settings.Capture.LastTargetProcessName;
        if (string.IsNullOrEmpty(last)) { ShowStatus("Pravým klikem vyber okno"); return; }
        var match = _enumerator.EnumerateVisibleWindows().FirstOrDefault(w => w.ProcessName.Equals(last, StringComparison.OrdinalIgnoreCase));
        if (match == null) { ShowStatus($"'{last}' není spuštěné — pravým vyber jiné"); return; }
        SetTarget(match);
    }

    private void SetTarget(WindowInfo info)
    {
        _currentTarget = info;
        HideStatus();
        _capture.Start(info.Handle, _settings.Capture.Fps);
        _settings.Capture.LastTargetProcessName = info.ProcessName;
        _settings.Capture.LastTargetWindowTitle = info.Title;
        PersistSettings();
    }

    private void ShowStatus(string text)
    {
        StatusText.Text = text;
        StatusOverlay.Visibility = Visibility.Visible;
    }
    private void HideStatus() => StatusOverlay.Visibility = Visibility.Collapsed;

    private void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        // Refresh window submenu
        WindowsMenu.Items.Clear();
        foreach (var w in _enumerator.EnumerateVisibleWindows())
        {
            var captured = w;
            var item = new MenuItem { Header = $"[{w.ProcessName}] {Truncate(w.Title, 50)}", Tag = w };
            if (w.Icon != null) item.Icon = new System.Windows.Controls.Image { Source = w.Icon, Width = 16, Height = 16 };
            item.Click += (_, _) => SetTarget(captured);
            WindowsMenu.Items.Add(item);
        }
        PauseItem.IsChecked = _capture.IsPaused;
        if (_currentTarget != null)
        {
            MuteItem.IsChecked = _audio.IsMuted(_currentTarget.ProcessId);
        }
        foreach (MenuItem item in FpsMenu.Items)
            item.IsChecked = (int)item.Tag! == _settings.Capture.Fps;
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";

    private void OnPickWindow(object sender, RoutedEventArgs e) => BeginPickWindow();
    private void BeginPickWindow()
    {
        _picker?.Dispose();
        _picker = new WindowPicker();
        ShowStatus("Klikni na okno které chceš sledovat...");
        _picker.BeginPick(hwnd =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                HideStatus();
                if (hwnd == IntPtr.Zero) return;
                var info = _enumerator.EnumerateVisibleWindows().FirstOrDefault(w => w.Handle == hwnd);
                if (info != null) SetTarget(info);
                else ShowStatus("Nepodařilo se najít vybrané okno");
            });
        });
    }

    private void OnFpsClick(object sender, RoutedEventArgs e)
    {
        var fps = int.Parse(((MenuItem)sender).Tag!.ToString()!);
        _settings.Capture.Fps = fps;
        _capture.SetFps(fps);
        PersistSettings();
    }

    private void OnPauseClick(object sender, RoutedEventArgs e) => TogglePause();
    private void TogglePause()
    {
        if (_capture.IsPaused) { _capture.Resume(); HideStatus(); }
        else { _capture.Pause(); ShowStatus("Paused"); }
        PauseItem.IsChecked = _capture.IsPaused;
    }

    private void OnMuteClick(object sender, RoutedEventArgs e) => ToggleMute();
    private void ToggleMute()
    {
        if (_currentTarget == null) return;
        var muted = _audio.ToggleMute(_currentTarget.ProcessId);
        MuteItem.IsChecked = muted;
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settings) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            PersistSettings();
            // re-register hotkeys
            _hotkeys?.Dispose();
            InitHotkeys();
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void PersistSettings()
    {
        _settings.Window.X = (int)Left;
        _settings.Window.Y = (int)Top;
        _settings.Window.Width = (int)Width;
        _settings.Window.Height = (int)Height;
        _store.Save(_settings);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        PersistSettings();
        _hotkeys?.Dispose();
        _picker?.Dispose();
        _capture.Dispose();
        _audio.Dispose();
    }
}
```

- [ ] **Step 3: Build (SettingsWindow ještě neexistuje — dočasně zakomentuj OnSettingsClick body)**

Edit `src/MiniPreview/UI/PreviewWindow.xaml.cs` — v `OnSettingsClick` dočasně:

```csharp
    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        // TODO Task 17: SettingsWindow dialog
        MessageBox.Show("Settings dialog — TODO Task 17", "MiniPreview");
    }
```

Run:

```powershell
dotnet build
```

Expected: 0 errors.

- [ ] **Step 4: Smoke test — spusť appku**

```powershell
dotnet run --project src/MiniPreview/MiniPreview.csproj
```

Expected:
- Okno se objeví top-most, transparentní okraj
- Pravým klikem → menu s "Vyber okno", FPS, atd.
- Vyber libovolné okno → uvidíš live náhled (5fps)
- Drag levým tlačítkem funguje
- Resize roh vpravo dole funguje
- Hotkey Ctrl+Alt+P pauzuje, Ctrl+Alt+M mute, Ctrl+Alt+L picker

Pokud něco nefunguje, zalogovat do issue (ne hard fail teď — v dalším tasku visual-verify).

- [ ] **Step 5: Commit**

```powershell
git add src/MiniPreview/UI/PreviewWindow.xaml src/MiniPreview/UI/PreviewWindow.xaml.cs
git commit -m "feat(ui): PreviewWindow with drag, resize, context menu, capture wiring"
```

---

## Task 17: SettingsWindow (hotkey editor + autostart)

**Files:**
- Create: `src/MiniPreview/UI/SettingsWindow.xaml`
- Create: `src/MiniPreview/UI/SettingsWindow.xaml.cs`
- Modify: `src/MiniPreview/UI/PreviewWindow.xaml.cs` (odstraň TODO MessageBox)

- [ ] **Step 1: Create XAML**

Write to `src/MiniPreview/UI/SettingsWindow.xaml`:

```xml
<Window x:Class="MiniPreview.UI.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml/2006"
        Title="MiniPreview — Nastavení"
        Width="480" Height="360"
        WindowStartupLocation="CenterOwner">
    <DockPanel Margin="12">
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button Content="OK" Width="80" Margin="4" IsDefault="True" Click="OnOk"/>
            <Button Content="Cancel" Width="80" Margin="4" IsCancel="True"/>
        </StackPanel>
        <StackPanel>
            <GroupBox Header="Hotkeys" Margin="0,0,0,12" Padding="8">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="120"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
                    <TextBlock Grid.Row="0" Grid.Column="0" Text="Pauza:" VerticalAlignment="Center"/>
                    <TextBox  Grid.Row="0" Grid.Column="1" x:Name="PauseHotkey" Margin="0,4"/>
                    <TextBlock Grid.Row="1" Grid.Column="0" Text="Mute:" VerticalAlignment="Center"/>
                    <TextBox  Grid.Row="1" Grid.Column="1" x:Name="MuteHotkey" Margin="0,4"/>
                    <TextBlock Grid.Row="2" Grid.Column="0" Text="Picker:" VerticalAlignment="Center"/>
                    <TextBox  Grid.Row="2" Grid.Column="1" x:Name="PickerHotkey" Margin="0,4"/>
                </Grid>
            </GroupBox>
            <TextBlock TextWrapping="Wrap" FontSize="11" Foreground="Gray" Margin="0,0,0,12">
                Formát: "Ctrl+Alt+P", "Shift+F12", atd. Modifikátory: Ctrl, Alt, Shift, Win. Klávesa: A-Z, 0-9, F1-F24.
            </TextBlock>
            <CheckBox x:Name="AutostartBox" Content="Spustit s Windows" Margin="0,8"/>
            <CheckBox x:Name="AlwaysOnTopBox" Content="Vždy nahoře" Margin="0,4"/>
        </StackPanel>
    </DockPanel>
</Window>
```

- [ ] **Step 2: Create code-behind**

Write to `src/MiniPreview/UI/SettingsWindow.xaml.cs`:

```csharp
using System.Windows;
using MiniPreview.Hotkeys;
using MiniPreview.Settings;

namespace MiniPreview.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsRoot _settings;
    private readonly AutostartManager _autostart = new();

    public SettingsWindow(SettingsRoot settings)
    {
        InitializeComponent();
        _settings = settings;

        PauseHotkey.Text = FormatBinding(_settings.Hotkeys.TogglePause);
        MuteHotkey.Text = FormatBinding(_settings.Hotkeys.ToggleMute);
        PickerHotkey.Text = FormatBinding(_settings.Hotkeys.OpenPicker);
        AutostartBox.IsChecked = _autostart.IsEnabled;
        AlwaysOnTopBox.IsChecked = _settings.Window.AlwaysOnTop;
    }

    private static string FormatBinding(HotkeyBinding b) => string.Join("+", b.Modifiers.Append(b.Key));

    private void OnOk(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.Hotkeys.TogglePause = ParseBinding(PauseHotkey.Text);
            _settings.Hotkeys.ToggleMute  = ParseBinding(MuteHotkey.Text);
            _settings.Hotkeys.OpenPicker  = ParseBinding(PickerHotkey.Text);
            // Validate parsable
            HotkeyDefinition.Parse(_settings.Hotkeys.TogglePause.Modifiers, _settings.Hotkeys.TogglePause.Key);
            HotkeyDefinition.Parse(_settings.Hotkeys.ToggleMute.Modifiers, _settings.Hotkeys.ToggleMute.Key);
            HotkeyDefinition.Parse(_settings.Hotkeys.OpenPicker.Modifiers, _settings.Hotkeys.OpenPicker.Key);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Chyba ve formátu hotkey: {ex.Message}", "MiniPreview", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (AutostartBox.IsChecked == true) _autostart.Enable();
        else _autostart.Disable();
        _settings.Autostart = AutostartBox.IsChecked == true;
        _settings.Window.AlwaysOnTop = AlwaysOnTopBox.IsChecked == true;

        DialogResult = true;
        Close();
    }

    private static HotkeyBinding ParseBinding(string s)
    {
        var parts = s.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) throw new ArgumentException("Prázdný hotkey");
        return new HotkeyBinding
        {
            Modifiers = parts.Take(parts.Length - 1).ToArray(),
            Key = parts[^1]
        };
    }
}
```

- [ ] **Step 3: Restore PreviewWindow.OnSettingsClick**

Edit `src/MiniPreview/UI/PreviewWindow.xaml.cs` — vrať plnou implementaci (odstraň TODO MessageBox):

```csharp
    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settings) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            PersistSettings();
            Topmost = _settings.Window.AlwaysOnTop;
            _hotkeys?.Dispose();
            InitHotkeys();
        }
    }
```

- [ ] **Step 4: Build + smoke**

```powershell
dotnet build
dotnet run --project src/MiniPreview/MiniPreview.csproj
```

Expected: dialog se otevře, zobrazí current hodnoty, OK uloží, Cancel zahodí.

- [ ] **Step 5: Commit**

```powershell
git add src/MiniPreview/UI/SettingsWindow.xaml src/MiniPreview/UI/SettingsWindow.xaml.cs src/MiniPreview/UI/PreviewWindow.xaml.cs
git commit -m "feat(ui): SettingsWindow with hotkey editor + autostart toggle"
```

---

## Task 18: Refine --selftest CLI (route v Main, ne v OnStartup)

Pozn: `App.OnStartup` přijde po WPF init což znamená že `--selftest` mode vytváří WPF Application zbytečně. Pro čistý CLI selftest udělej vlastní `Main` který Application vůbec nevytvoří pokud je `--selftest`.

**Files:**
- Modify: `src/MiniPreview/MiniPreview.csproj` (přidat custom Main)
- Create: `src/MiniPreview/Program.cs`
- Modify: `src/MiniPreview/App.xaml` (přidat `x:Class` build action)

- [ ] **Step 1: Disable auto-generated Main + add explicit one**

Edit `src/MiniPreview/MiniPreview.csproj` — přidej do `<PropertyGroup>`:

```xml
    <StartupObject>MiniPreview.Program</StartupObject>
```

(Pokud `App.xaml` má `Build Action = ApplicationDefinition`, .NET generuje `Main`. Tímto `StartupObject` přepíšeme.)

- [ ] **Step 2: Create Program.cs**

Write to `src/MiniPreview/Program.cs`:

```csharp
namespace MiniPreview;

internal static class Program
{
    [System.STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--selftest")
        {
            return SelfTest.SelfTestRunner.Run();
        }

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
```

Pokud build vyhlásí "App.InitializeComponent nedostupné" — App.xaml musí mít `Build Action = ApplicationDefinition`. To je v WPF SDK default; pokud ne, edituj `MiniPreview.csproj` a explicitně:

```xml
  <ItemGroup>
    <ApplicationDefinition Include="App.xaml"/>
  </ItemGroup>
```

- [ ] **Step 3: Remove --selftest router z App.OnStartup**

Edit `src/MiniPreview/App.xaml.cs` — odstraň:

```csharp
        if (e.Args.Length > 0 && e.Args[0] == "--selftest")
        {
            var exitCode = SelfTestRunner.Run();
            Shutdown(exitCode);
            return;
        }
```

(Argument routing je teď v `Program.Main`.)

- [ ] **Step 4: Build + smoke selftest**

```powershell
dotnet build
dotnet run --project src/MiniPreview/MiniPreview.csproj -- --selftest
```

Expected: JSON na stdout, žádné WPF okno se neobjeví, exit code 0 (na zdravém systému).

- [ ] **Step 5: Commit**

```powershell
git add src/MiniPreview/Program.cs src/MiniPreview/MiniPreview.csproj src/MiniPreview/App.xaml.cs
git commit -m "refactor(bootstrap): explicit Main with --selftest CLI router"
```

---

## Task 19: tools/smoke-test.ps1 + tools/visual-verify.ps1

**Files:**
- Create: `tools/smoke-test.ps1`
- Create: `tools/visual-verify.ps1`

- [ ] **Step 1: Create smoke-test.ps1**

Write to `tools/smoke-test.ps1`:

```powershell
# Spusti --selftest a overi JSON output
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot\..
try {
    Write-Host "Building..."
    dotnet build src/MiniPreview/MiniPreview.csproj -c Release -v minimal | Out-Null

    $exe = "src/MiniPreview/bin/Release/net8.0-windows10.0.19041.0/MiniPreview.exe"
    if (-not (Test-Path $exe)) { throw "Build neprodukoval $exe" }

    Write-Host "Running --selftest..."
    $jsonOutput = & $exe --selftest
    $exitCode = $LASTEXITCODE

    Write-Host $jsonOutput
    $report = $jsonOutput | ConvertFrom-Json

    if (-not $report.probesOk) { throw "Probes failed" }
    if (-not $report.captureOk) { throw "Capture failed: $($report.captureError)" }
    if (-not $report.audioOk)   { throw "Audio failed: $($report.audioError)" }
    if ($report.framesPerSec -lt 1) { throw "FPS too low: $($report.framesPerSec)" }

    Write-Host "Smoke test PASS (exit=$exitCode, fps=$($report.framesPerSec))"
    exit 0
} finally {
    Pop-Location
}
```

- [ ] **Step 2: Create visual-verify.ps1**

Write to `tools/visual-verify.ps1`:

```powershell
# Spusti Notepad + MiniPreview a vezme screenshot
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot\..
try {
    $exe = "src/MiniPreview/bin/Release/net8.0-windows10.0.19041.0/MiniPreview.exe"
    if (-not (Test-Path $exe)) {
        Write-Host "Building..."
        dotnet build src/MiniPreview/MiniPreview.csproj -c Release -v minimal | Out-Null
    }

    Write-Host "Starting Notepad..."
    $np = Start-Process notepad.exe -PassThru
    Start-Sleep -Seconds 1
    # Send some text via SendKeys
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.SendKeys]::SendWait("MiniPreview test {DATETIME}")

    Write-Host "Starting MiniPreview..."
    $mp = Start-Process $exe -PassThru
    Start-Sleep -Seconds 3

    Write-Host "Taking screenshot..."
    Add-Type -AssemblyName System.Drawing
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)

    $outDir = "tools/screenshots"
    if (-not (Test-Path $outDir)) { New-Item -ItemType Directory $outDir | Out-Null }
    $ts = Get-Date -Format "yyyyMMdd-HHmmss"
    $outPath = Join-Path $outDir "verify-$ts.png"
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $gfx.Dispose(); $bmp.Dispose()

    Write-Host "Screenshot saved: $outPath"
    Write-Host "Cleaning up..."
    try { $mp.Kill() } catch {}
    try { $np.Kill() } catch {}
} finally {
    Pop-Location
}
```

- [ ] **Step 3: Run smoke test**

```powershell
.\tools\smoke-test.ps1
```

Expected: build OK, selftest PASS s rozumným framesPerSec (>1), exit 0.

- [ ] **Step 4: Commit**

```powershell
git add tools/smoke-test.ps1 tools/visual-verify.ps1
git commit -m "tools: PowerShell smoke + visual verification scripts"
```

---

## Task 20: GitHub Actions CI

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: Create workflow**

Write to `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build -c Release --no-restore

      - name: Test (unit only, skip RequiresDesktop)
        run: dotnet test -c Release --no-build --filter "Category!=RequiresDesktop" --logger "console;verbosity=minimal"
```

- [ ] **Step 2: Commit + push**

```powershell
git add .github/workflows/ci.yml
git commit -m "ci: add GitHub Actions workflow for build + unit tests"
git push origin main
```

Expected: na GitHubu se objeví běžící workflow, mělo by skončit zelená.

---

## Self-review checklist (po dokončení všech tasků)

- [ ] Všechny `dotnet test --filter "Category!=RequiresDesktop"` testy passují
- [ ] `dotnet run --project src/MiniPreview/MiniPreview.csproj` spustí GUI
- [ ] `dotnet run --project src/MiniPreview/MiniPreview.csproj -- --selftest` vrátí 0 a JSON
- [ ] `.\tools\smoke-test.ps1` projde
- [ ] `.\tools\visual-verify.ps1` vyrobí screenshot v `tools/screenshots/`
- [ ] Pravým klikem na preview → menu, FPS, Pauza, Mute, Pick window — vše funguje
- [ ] Drag levým + resize roh fungují
- [ ] Hotkeys Ctrl+Alt+P/M/L fungují i když je vepředu jiná appka
- [ ] Pozice/velikost/FPS se ukládají do `%APPDATA%\MiniPreview\settings.json`
- [ ] CI na GitHubu zelený
