# MiniPreview

Lehká Windows app na floating live thumbnail náhled vybraného okna.

**Stack:** C# + WPF + .NET 8 + `Windows.Graphics.Capture` + NAudio.

**Co umí:**
- Floating top-most miniaturní náhled libovolného okna (fullscreen borderless hry, windowed apps, monitory).
- Pauza/resume náhledu — kompletně uvolní capture session, žádný FPS hit ve hrách.
- Nastavitelné FPS náhledu (1 / 2 / 5 / 10 / 15 / 30, default 5).
- Per-process mute/unmute target appky.
- Globální hotkeys (Ctrl+Alt+P pauza, Ctrl+Alt+M mute, Ctrl+Alt+L picker — customizovatelné).
- Feature checker pro WinHance-debloated Windows (detekuje chybějící služby/komponenty + ukáže fix).

**Status:** Design phase. Viz [`docs/superpowers/specs/2026-05-27-mini-preview-design.md`](docs/superpowers/specs/2026-05-27-mini-preview-design.md).

**Repo:** private.
