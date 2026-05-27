# MiniPreview — Design Spec

**Datum:** 2026-05-27
**Autor:** iamLukyy (via Claude)
**Status:** Draft — pending user approval

---

## 1. Účel a cíl

Lehká Windows aplikace, která zobrazuje **live miniaturní náhled** vybraného okna jiné aplikace (typicky fullscreen borderless hry, ale i windowed apek) ve **floating top-most okně** vedle/uvnitř hlavního monitoru. Uživatel může:

- Vybrat target okno ze seznamu nebo kliknutím "Pick".
- **Pauzovat / resume** live náhled (aby capture nezatěžoval ve hrách na 100 %).
- **Měnit FPS** náhledu (1, 2, 5, 10, 15, 30 — default 5).
- **Mutovat / unmutovat** target aplikaci (per-process, ne system-wide).
- Drag & resize okna náhledu kamkoli na obrazovce (pozice/velikost se ukládá).

Klíčový non-funkční požadavek: **minimální FPS hit ve sledované hře**. Splněno použitím `Windows.Graphics.Capture` (WGC), který běží přes desktop composition path — to samé API co Game Bar a OBS.

Bonus: **feature checker** detekuje při startu chybějící Windows komponenty (důsledek WinHance debloateru) a zobrazí přesný PowerShell příkaz k obnově. Žádná automatická instalace bez vědomí uživatele.

---

## 2. Architektura

### 2.1 Stack

- **.NET 8** (Windows Desktop runtime)
- **WPF** (XAML UI)
- **CsWinRT** projection pro `Windows.Graphics.Capture` a další WinRT API
- **NAudio** pro per-process audio mute (Core Audio APIs wrapper)
- **Win32 P/Invoke** pro hotkeys (`RegisterHotKey`), enumeraci oken (`EnumWindows`, `WindowFromPoint`), ikony procesů (`SHGetFileInfo`), autostart (registry `HKCU\...\Run`)

Žádná další NuGet závislost mimo NAudio a `Microsoft.Windows.CsWinRT` (pokud není už zahrnut v target frameworku `net8.0-windows10.0.19041.0`).

### 2.2 Capture pipeline

```
[Target HWND/HMONITOR]
       │
       ▼
GraphicsCaptureItem (via IGraphicsCaptureItemInterop.CreateForWindow)
       │
       ▼
Direct3D11CaptureFramePool.CreateFreeThreaded(d3dDevice, B8G8R8A8, 2, size)
       │  ── FrameArrived event (async, free-threaded) ───┐
       │                                                   │
       │  pacing gate: drop pokud čas < (1000ms / fps)    │
       │                                                   ▼
       │                                          [GPU texture]
       │                                                   │
       │                                       D3D11.CopyResource → staging tex
       │                                       D3D11.Map (Read) → bytes
       │                                                   │
       │                                                   ▼
       │                                  Dispatcher.BeginInvoke(BackgroundPriority):
       │                                       WriteableBitmap.WritePixels(...)
       │                                                   │
       └─ Recreate(...) když uživatel resizuje target ────┘
```

Klíčové vlastnosti:
- **Free-threaded frame pool** = WGC nehází frame na UI thread; pacing & readback běží na pool threadu, UI thread jen dělá `WritePixels` (velmi rychlé).
- **Pacing dropem**, ne změnou frame intervalu WGC — WGC nemá throttle API, takže jen ignorujeme frames které přišly příliš brzy. Vedlejší benefit: target appka neví že někdo "skipuje", takže nemá FPS hit z nastavování capture rate.
- **CPU readback** je u thumbnailu (≤ ~400×300 @ ≤ 30fps) bezvýznamný. Vyhneme se SharpDX / Vortice / D3DImage interop složitostem.
- **Pause** = `framePool.Dispose()` + uvolnění D3D device. Resume = recreate. Capture session úplně mizí → 0 % overhead.

### 2.3 Audio mute pipeline

```
NAudio:
  MMDeviceEnumerator.GetDefaultAudioEndpoint(Render, Multimedia)
      ↓
  device.AudioSessionManager.Sessions  (refresh on each toggle — sessions přicházejí/odcházejí)
      ↓
  filter where session.GetProcessID() == targetPid
      ↓
  for each matching session: session.SimpleAudioVolume.Mute = !state
```

Poznámky:
- Target PID získáme z `GetWindowThreadProcessId(targetHwnd, out pid)` při výběru okna.
- Některé hry/appky mají víc audio sessionů (child procesy, audio engine v zvláštním PID). Mute aplikujeme na **všechny sessiony s odpovídajícím PID i s odpovídajícím parent PID**.
- Mute stav je nezávislý na pause/resume náhledu. Příkaz "mute" funguje i když je preview pozastavený.

### 2.4 Hotkey pipeline

```
Hidden message-only HWND (HWND_MESSAGE)
      ↓
RegisterHotKey(hwnd, id, modifiers, vk) pro každý nakonfigurovaný hotkey
      ↓
WndProc chytá WM_HOTKEY → dispatch do C# eventu na UI threadu
```

Defaultní hotkeys (customizovatelné v Settings):
- `Ctrl+Alt+P` — toggle pause/resume náhledu
- `Ctrl+Alt+M` — toggle mute target appky
- `Ctrl+Alt+L` — otevřít picker (list oken)

### 2.5 Komponenty

| Komponenta | Soubor | Závislosti | Veřejné API |
|---|---|---|---|
| `CaptureService` | `Capture/CaptureService.cs` | WGC, D3D11 | `Start(hwnd, fps)`, `Stop()`, `SetFps(int)`, `Pause()`, `Resume()`, event `FrameReady(WriteableBitmap)` |
| `AudioMuteService` | `Audio/AudioMuteService.cs` | NAudio | `ToggleMute(pid)`, `IsMuted(pid)`, `Refresh()` |
| `WindowEnumerator` | `Windows/WindowEnumerator.cs` | User32, Shell32 | `EnumerateVisibleWindows() → IEnumerable<WindowInfo>` |
| `WindowPicker` | `Windows/WindowPicker.cs` | User32 (LL mouse hook) | `BeginPick(callback)`, `Cancel()` |
| `HotkeyManager` | `Hotkeys/HotkeyManager.cs` | User32 | `Register(id, mods, vk, action)`, `Unregister(id)`, `UnregisterAll()` |
| `FeatureChecker` | `Bootstrap/FeatureChecker.cs` | — | `RunProbes() → ProbeResult[]` |
| `SettingsStore` | `Settings/SettingsStore.cs` | System.Text.Json | `Load()`, `Save(settings)`; cesta `%APPDATA%\MiniPreview\settings.json` |
| `AutostartManager` | `Settings/AutostartManager.cs` | Registry | `IsEnabled`, `Enable()`, `Disable()` |
| `PreviewWindow` | `UI/PreviewWindow.xaml(.cs)` | WPF | UI shell — top-most borderless, drag, resize, context menu, status indikátory |
| `App` | `App.xaml(.cs)` | — | Bootstrap: feature check → load settings → ukázat okno |

Každý soubor má jeden důvod existence. `PreviewWindow` je orchestrátor (drží reference na services); business logika je v service třídách.

---

## 3. Data flow — typický scénář

1. **Start aplikace** → `App.OnStartup`:
   1. `FeatureChecker.RunProbes()`. Pokud něco failne → dialog s návodem → exit nebo "Try anyway".
   2. `SettingsStore.Load()` → obnovi pozici/velikost okna, last FPS, last target process name, hotkey bindings.
   3. `HotkeyManager.RegisterAll()` z nastavení.
   4. `PreviewWindow.Show()`. Pokud `lastTargetProcessName` najdeme v aktuálních oknech, rovnou začneme capture; jinak zobrazíme placeholder + "Vyberte okno".
2. **Uživatel klikne pravým na náhled** → kontextové menu:
   - "Vyberte okno ▸" submenu = `WindowEnumerator.EnumerateVisibleWindows()` (lazy při otevření menu).
   - "Pick window..." → `WindowPicker.BeginPick(...)`, cursor change, kliknutí mimo náhled vybere top-level HWND pod kurzorem.
   - "FPS ▸ 1 / 2 / 5 / 10 / 15 / 30".
   - "Pauza" (zobrazí ✓ když pauzováno).
   - "Mute target" (zobrazí ✓ když muted).
   - "Nastavení..." → settings dialog (hotkeys, autostart).
3. **Uživatel vybere okno** → `CaptureService.Start(hwnd, fps)` → frames začnou téct → `FrameReady` event → `PreviewWindow` updatuje `Image.Source` na `WriteableBitmap`.
4. **Uživatel stiskne Ctrl+Alt+P** → `HotkeyManager` zavolá akci → `CaptureService.Pause()` (frame pool disposed) → UI ukáže overlay "Paused".
5. **Target okno se zavře** → WGC vyhodí `GraphicsCaptureItem.Closed` event → `CaptureService` to zachytí → notifikuje `PreviewWindow` → ukáže "Target lost — Vyberte okno".

---

## 4. Error handling

| Situace | Detekce | Akce |
|---|---|---|
| `GraphicsCaptureSession.IsSupported() == false` | Při startu | Dialog: "Windows.Graphics.Capture není dostupné. Vyžaduje Windows 10 1903+." |
| `MMDeviceEnumerator` hodí COMException | Při startu | Dialog: "Core Audio API nedostupné. Zkontroluj `Get-Service Audiosrv`." + PowerShell `Set-Service -Name Audiosrv -StartupType Automatic; Start-Service Audiosrv`. |
| Target okno zavřeno během capture | `GraphicsCaptureItem.Closed` event | Stop capture, ukázat placeholder. |
| Target okno přesměrováno (Win+D, minimalizace) | Frames přestanou chodit; D3D texture velikost = 0 | Pokračovat (poslední frame zůstane viditelný), po 30 s ukázat "No frames". |
| Mute toggle — žádné sessiony s daným PID | NAudio enumerate vrátí 0 hit | Status notifikace: "Tato appka momentálně nepřehrává zvuk". |
| Hotkey kolize (jiná appka zaregistrovala stejný) | `RegisterHotKey` vrátí false | Settings dialog zvýrazní červeně + tooltip s návodem. |
| Settings file corrupt | JSON parse exception | Zalogovat, použít defaults, přejmenovat na `settings.json.bak`. |
| Crash v capture pipeline | unhandled exception | Try/catch v `FrameArrived` handleru — chybu zalogovat do `%APPDATA%\MiniPreview\error.log`, ukázat toast, NEvypadnout. |

---

## 5. Feature checker — co se kontroluje

Probes při startu, každý vrátí `ProbeResult { Name, Ok, Detail, FixCommand }`:

1. **WGC dostupné** — `GraphicsCaptureSession.IsSupported()`. Fix: žádný v rámci WinHance — pravděpodobně by vyžadovalo přeinstalaci Windows. Pokud false, appka nemůže fungovat, exit.
2. **Audio service běží** — `Get-Service Audiosrv`. Fix: `Set-Service -Name Audiosrv -StartupType Automatic; Start-Service Audiosrv` (admin).
3. **DWM (Desktop Window Manager) běží** — `Get-Service uxsms`. Fix: `Set-Service -Name uxsms -StartupType Automatic; Start-Service uxsms` (admin). WGC bez DWM nefunguje.
4. **.NET 8 Desktop Runtime** — implicit; pokud chybí, appka se vůbec nespustí (.NET runtime ukáže vlastní dialog).
5. **App permissions: Capture** — od Windows 11 22H2 některé buildy vyžadují souhlas. Probe: zkusíme zavolat `GraphicsCapturePicker` placeholder. Pokud `UnauthorizedAccessException` → "Povolte capture v Settings → Privacy → Screen recording".

Dialog má:
- Sekci "OK" (zelená) a "Problém" (červená).
- Pro každý problém: popisek + kód s PowerShell příkazem + tlačítko "Copy" + odkaz na docs.
- Tlačítka: "Recheck", "Try anyway" (pokud aspoň WGC OK), "Exit".

---

## 6. Testing strategie

### 6.1 Unit testy (`xUnit` + `NSubstitute` mocks)

- `FeatureChecker` — mock service probes, ověř že FixCommand obsahuje očekávaný `Add-WindowsCapability`/`Set-Service`.
- `SettingsStore` — round-trip serializace, missing file → defaults, corrupt file → backup + defaults.
- `WindowEnumerator` — mock `EnumWindows` callback, ověř filtry (skip invisible, skip empty title, skip tool windows).
- `HotkeyManager` — mock `RegisterHotKey`, ověř že kolize se reportuje, unregister funguje.

### 6.2 Integration testy

- `CaptureService` proti reálnému Notepadu spuštěnému jako fixture. Asserty:
  - Frame přijde do 2 s.
  - Po `Pause()` žádný další frame za 1 s.
  - Po `SetFps(2)` rate ≤ 2.5 frames/s (s tolerancí).
  - Po zavření Notepadu vyletí `TargetLost` event.
- `AudioMuteService` — spustí `winmm` test tón (krátký WAV), ověří mute toggle změní `SimpleAudioVolume.Mute`.

### 6.3 CLI smoke test (`MiniPreview.exe --selftest`)

Speciální mód: appka neukáže UI, jen:
1. Spustí `FeatureChecker.RunProbes()`.
2. Spustí krátký capture loop proti `explorer.exe` shell window (vždy existuje).
3. Vypíše JSON na stdout: `{"probes": [...], "captureOk": true, "audioOk": true, "framesPerSec": 4.8}`.
4. Exit code 0 = vše OK, 1 = něco failne.

Použito v PS skriptu `tools/smoke-test.ps1`:
```powershell
$result = & .\MiniPreview.exe --selftest | ConvertFrom-Json
if ($result.captureOk -ne $true) { throw "Capture failed" }
```

### 6.4 Manuální verifikace přes screenshot

`tools/visual-verify.ps1`:
1. Spustí Notepad, napíše do něj unikátní string.
2. Spustí MiniPreview, předá `--target-pid <notepad-pid>` (CLI flag pro testing).
3. Počká 2 s, vezme screenshot celé obrazovky (`System.Windows.Forms.Screen` + `Graphics.CopyFromScreen`).
4. Save do `tools/screenshots/<timestamp>.png`. Použiji to v Claude session na manuální verifikaci (vidím obrázek).

### 6.5 CI

GitHub Actions workflow `.github/workflows/ci.yml`:
- `windows-latest` runner
- `dotnet build`, `dotnet test`
- (Capture testy nepoběží v headless runneru — označit `[Trait("Category","RequiresDesktop")]` a skipnout v CI.)

---

## 7. Settings schema (`%APPDATA%\MiniPreview\settings.json`)

```json
{
  "version": 1,
  "window": {
    "x": 100,
    "y": 100,
    "width": 320,
    "height": 240,
    "alwaysOnTop": true
  },
  "capture": {
    "fps": 5,
    "lastTargetProcessName": "chrome.exe",
    "lastTargetWindowTitle": "GitHub — Chrome"
  },
  "hotkeys": {
    "togglePause":  { "modifiers": ["Ctrl","Alt"], "key": "P" },
    "toggleMute":   { "modifiers": ["Ctrl","Alt"], "key": "M" },
    "openPicker":   { "modifiers": ["Ctrl","Alt"], "key": "L" }
  },
  "autostart": false
}
```

---

## 8. Repo layout

```
MiniPreview/
├── MiniPreview.sln
├── src/
│   └── MiniPreview/
│       ├── MiniPreview.csproj          (net8.0-windows10.0.19041.0, WPF)
│       ├── App.xaml / App.xaml.cs
│       ├── UI/PreviewWindow.xaml(.cs)
│       ├── UI/SettingsWindow.xaml(.cs)
│       ├── UI/FeatureCheckDialog.xaml(.cs)
│       ├── Capture/CaptureService.cs
│       ├── Capture/D3D11Helpers.cs
│       ├── Audio/AudioMuteService.cs
│       ├── Windows/WindowEnumerator.cs
│       ├── Windows/WindowPicker.cs
│       ├── Hotkeys/HotkeyManager.cs
│       ├── Bootstrap/FeatureChecker.cs
│       └── Settings/SettingsStore.cs
├── tests/
│   └── MiniPreview.Tests/
│       └── *.cs
├── tools/
│   ├── smoke-test.ps1
│   └── visual-verify.ps1
├── docs/
│   └── superpowers/specs/2026-05-27-mini-preview-design.md  (tento dokument)
├── .github/workflows/ci.yml
├── .gitignore  (VS, Rider, bin, obj)
└── README.md
```

---

## 9. Out of scope (záměrně NE)

- **Audio mux do preview**: žádný streaming audia z target appky do MiniPreview. Mute je jen on/off systémové sessione.
- **Recording**: žádné nahrávání. Jen live preview.
- **Multi-window preview**: jeden target naráz. (Můžem v budoucnu otevřít víc instancí appky.)
- **Cross-platform**: Windows only. WGC neexistuje jinde.
- **Touch / pen input**: jen klávesnice + myš.
- **Auto-update**: ručně přes `git pull` + `dotnet build`.

---

## 10. Otevřené otázky pro implementační plán

1. Volba mezi pure P/Invoke D3D11 vs. tenkým wrapperem (např. `Vortice.Direct3D11` jako single dependency)? Pure P/Invoke je čistší, Vortice rychlejší vývoj. — Rozhodnout ve fázi writing-plans.
2. WPF rendering: `WriteableBitmap` (CPU) vs `D3DImage` (GPU sharing)? Pro thumbnail @ ≤30fps stačí WriteableBitmap; D3DImage je optimalizace. Začneme WriteableBitmap, ev. přejdeme později.
3. Lokalizace UI: CS, EN, oboje? Default CS (autor používá CS), EN přidat až bude poptávka.

Tyhle tři zůstávají záměrně otevřené — patří do plánu, ne specu.
