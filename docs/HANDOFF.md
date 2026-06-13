# MakroPad Handoff

Last updated: 2026-06-13

## Current State

MakroPad has a working vertical prototype across firmware and desktop app:

- ESP32-S3 firmware reads 20 button inputs, supports up to 3 sliders, drives the
  SSD1306 OLED, speaks the serial protocol, and tracks host heartbeat state.
- The desktop app is an Avalonia/.NET app with tray behavior, simulator mode,
  real serial connections, auto-connect, config persistence, action execution,
  OLED config sync, Now Playing updates, and a dark sidebar UI.
- Config is stored at `%APPDATA%\MakroPad\config.json`.
- The serial protocol is documented in `docs/PROTOCOL.md`.
- Firmware wiring and pin choices are documented in `docs/HARDWARE.md`.
- Graphify outputs exist in `graphify-out/` and can be used for architecture
  orientation.

## Recent Milestones

- Rebuilt the desktop UI from scratch into a device-centric layout (Stream-Deck
  style). `ShellViewModel` is the new root (the former `MainWindowViewModel`
  orchestration was merged into it across `ShellViewModel.cs` and
  `ShellViewModel.Runtime.cs`); navigation is workflow-ordered: Pad, Display,
  Verbindung, Einstellungen, Diagnose. The Pad screen (`PadViewModel`,
  `PadView`) mirrors the hardware as a 20-key grid plus 3 slider strips with an
  Edit/Test mode toggle, an inline profile bar, live input highlighting (new
  `InputActivated` runtime event, raised for hardware and simulator inputs), and
  a contextual binding inspector that reuses `ActionEditorViewModel`. The
  Display screen keeps a persistent live OLED preview. A global Speichern button
  lives in the nav rail; theme toggle moved there too. All legacy views
  (`MacrosView`, `DeviceView`, `SimulationView`, `ProfilesSettingsView`,
  `ApplicationSettingsView`, `DisplayEditorView`, `DebugView`) and the
  `PageHeader` control were removed. The domain libraries (Core/Device/Actions)
  and their tests were untouched; `MakroPad.App.Tests` gained `PadViewModel`
  coverage.
- Restructured the desktop UI shell. The formerly monolithic `MainWindow.axaml`
  (~990 lines) is now a thin shell that hosts seven focused section views:
  `MacrosView`, `DeviceView`, `SimulationView`, `ProfilesSettingsView`,
  `ApplicationSettingsView`, `DisplayEditorView`, and `DebugView`, all bound to
  the shared `MainWindowViewModel`. A reusable `Controls/PageHeader` provides the
  per-section title/subtitle/profile-badge header. Shortcut recording and the
  open-app file picker that previously lived in `MainWindow.axaml.cs` now live in
  `MacrosView.axaml.cs` next to the action editor they serve.
- Added a light/dark theme system. `Themes/AppTheme.axaml` defines themed
  brushes via `ThemeDictionaries`, `Themes/NavIcons.axaml` provides nav/section
  icon geometries, and `App.axaml` registers FluentTheme Dark/Light palettes.
  `MainWindowViewModel` exposes `IsDarkTheme`, `ToggleThemeCommand`, and
  `ThemeToggleHint`, switching `Application.Current.RequestedThemeVariant` at
  runtime. Added a MakroPad app icon (`Assets/makropad.png` / `.ico`).
- Added Windows `app.volume` action for setting the volume of a configured
  process/audio session.
- Added configurable OLED default screen with 3-line or 5-line layout.
- Added placeholders: `{profile}`, `{status}`, `{time}`, `{date}`,
  `{media.artist}`, `{media.title}`, `{media.track}`, `{volume.system}`, and
  `{device.uptime}`.
- Added OLED preview in settings.
- Added active audio session list in the debug view and automatic refresh every
  few seconds.
- Specialized the action editor for common actions while keeping raw parameter
  editing available.
- Added Graphify project graph and agent workflow guidance.
- Started the `MainWindowViewModel` split by extracting audio session refresh
  state into `AudioSessionsViewModel` and OLED default screen rendering into
  `OledDefaultScreenRenderer`.
- Moved Now Playing polling, status, track state, and media display messages
  into `NowPlayingViewModel`.
- Moved device `config` message sync and OLED preview rendering coordination
  into `DeviceDisplaySyncService`.
- Split the formerly monolithic `MainWindowViewModel` into smaller app
  services and child view models. Device connection lifecycle now lives in
  `DeviceSessionService`, config load/save/migration in `MakroPadConfigService`,
  binding validation/action metadata in `BindingEditorService` and
  `ActionDefinitionCatalog`, action feedback in `ActionFeedbackCoordinator`,
  display-trigger runtime dispatch in `DisplayTriggerOrchestrator`, action
  editor state in `ActionEditorViewModel`, profile/binding editing in
  `ProfilesEditorViewModel`, display/OLED settings in
  `DisplaySettingsViewModel`, display-trigger editor state in
  `DisplayTriggersEditorViewModel`, and bounded diagnostics in
  `EventLogService`. Background audio/Now Playing loop startup and cancellation
  are owned by `AppRuntimeLoopService`. `MainWindowViewModel` is now primarily
  the UI/application coordinator.
- Added `MakroPad.App.Tests` to cover app services and extracted view models,
  including display-trigger orchestration, config lifecycle, action feedback,
  device-session state, and editor validation.
- Added a configurable app-side deadband for `system.volume` and `app.volume`
  slider actions to suppress adjacent-percent jitter. The default is 2
  percentage points and `0` disables the filter.
- Added configurable OLED display triggers for button and slider inputs. The
  app can render normal text or lightweight HTML-like content to OLED text lines
  and sends them as temporary `display;mode=template` messages.
- Added firmware-side horizontal scrolling for OLED text lines longer than the
  visible 21-character width. Display triggers and their Test button now send
  full rendered lines so long content can scroll on the device while the app
  preview remains a fixed-width snapshot.
- Added per-line OLED text sizes for temporary display templates via optional
  `lineXSize` protocol fields. App-side HTML display triggers map `h1`/`h2`
  content to size `2`; normal text and other HTML lines remain size `1`.
- Added per-line OLED text sizes for the configurable default screen. Settings
  can store size `1` or `2` for title and lines 1-4, and `config` sends
  optional `defaultTitleSize`/`defaultLineXSize` fields to firmware.
- Added HTML mode for OLED defaultscreen editing. The app renders lightweight
  HTML to text/default size fields before sending `config`, with `h1`/`h2`
  mapped to OLED size `2`; firmware scrolling now bases its cycle on each
  line's effective visible width so size `2` text scrolls when needed.
- Added the first structured OLED layout mode. Display triggers can now use
  `templateMode=layout`; the app sends `display;mode=layout` with semantic
  fields, and firmware renders `volume`, `media`, `action`, `profile`, and
  `status` screens with a header, simple drawn icons, horizontal text
  scrolling, and a progress bar for volume-style screens. The editor includes a
  structured layout preview, the Test button sends layout screens through the
  layout protocol path, and unchanged legacy default action/media triggers are
  migrated to `templateMode=layout`. The design notes live in
  `docs/DISPLAY_DESIGN.md`.
- Replaced the separate `OLED Default` and `Display Trigger` settings tabs with
  a unified `Display` editor. Idle-screen settings, display rules, selected
  rule editing, previews, and runtime display options now live in one workflow.
- Added defensive minimum sizes and horizontal scroll behavior to the main
  Avalonia UI so dense editor forms keep controls readable instead of
  overlapping when the window is made small.
- Hardened firmware slider reading with multi-sample trimmed averaging,
  stronger low-pass filtering, deadband hysteresis, endpoint snapping, and a
  minimum send interval. Hardware docs now recommend a 100 nF wiper-to-GND
  capacitor if a physical slider still jitters.
- Added a firmware HID fallback for app-disconnected operation. Without an app
  ACK, buttons 1-7 send basic media/volume consumer-control events and slider 1
  sends throttled relative system-volume HID nudges. App-specific volume remains
  app-side.

## Supported Actions

| Action | Parameters | Notes |
| --- | --- | --- |
| `media.playPause` | none | Windows media key |
| `media.nextTrack` | none | Windows media key |
| `media.previousTrack` | none | Windows media key |
| `media.stop` | none | Windows media key |
| `media.volumeUp` | none | Windows media key |
| `media.volumeDown` | none | Windows media key |
| `media.mute` | none | Windows media key |
| `system.volume` | none | Uses slider value `0..4095`, configurable deadband |
| `app.volume` | `process=Spotify` | Uses slider value, active audio sessions, configurable deadband |
| `system.openUrl` | `url=https://example.com` | Opens default browser |
| `system.openApp` | `path=notepad.exe` | Opens app/file via OS shell |
| `keyboard.shortcut` | `keys=Ctrl+Shift+Esc` | Sends supported key combinations |
| `log` | `message=Hello` | Writes to app log |

## OLED Behavior

- The app sends `config` messages after connect and when saving settings.
- `action-result` messages are shown temporarily.
- `actionResultTimeoutMs=0` disables automatic reset.
- With Now Playing active, action-result messages return to the media screen
  unless the default template itself contains `{media.*}` placeholders.
- If the default template contains `{media.*}`, Now Playing updates placeholder
  values without forcing the dedicated media screen.
- Display triggers are currently app-side settings. The editor now mirrors the
  macro binding pattern with a trigger list and Apply/Add/Remove controls.
  Slider triggers use a percentage-change threshold; button triggers display
  immediately. The editor also has a Test button that sends the current editor
  content to the connected OLED without waiting for a hardware input. Multiple
  enabled triggers for the same input are sent in list order as a timed display
  sequence, and the editor has Up/Down controls to reorder that sequence.
  Slider triggers support `any`, `inRange`, `increasing`, and `decreasing`
  conditions. The editor includes an app-side OLED preview and compact wrapping
  action buttons. Match, condition, and status controls are shown only when they
  apply to the selected trigger event type. Built-in presets, duplication,
  editable preview values, match diagnostics, content warnings, draft status,
  and device-width clipping make the editor suitable for normal use.
- Display trigger event types now include `input` and `actionResult`.
  Action-result triggers support `ok`/`failed` matching and action placeholders.
  Existing configs without action-result triggers receive configurable default
  Action/Action failed triggers. The legacy firmware `action-result` screen is
  only used as a fallback when no matching app-side trigger is available.
- Display trigger event types also include `mediaChanged`, with
  `{media.artist}`, `{media.title}`, and `{media.track}` placeholders. Existing
  configs receive a persistent default Media trigger once. The dedicated
  firmware media screen is now only a fallback when no enabled media trigger
  exists, while media state is still synchronized for default-screen
  placeholders and return behavior. Temporary action-result trigger sequences
  append the active media trigger so the configured Media screen returns.
- Display trigger event types also include `connectionChanged`, with status
  matching for connected, connecting, unresponsive, disconnected, or any and a
  `{connection.status}` placeholder. Firmware status screens remain the fallback
  when the transport is no longer writable.
- Display trigger event types also include `profileChanged`, which can show a
  custom screen after the active profile changes.
- Display trigger normalization, rendering, matching, and slider input tracking
  now live in `MakroPad.Core.DisplayTriggers` with unit tests. Slider thresholds
  accumulate independently per trigger, newer display events cancel older
  delayed sequences, and action-result screens are no longer immediately
  overwritten by input-trigger screens from the same input event.
- Legacy display-trigger defaults are persisted immediately after migration,
  and config writes use a temporary file plus atomic replacement.

## Known Limitations

- Now Playing currently reads the Spotify desktop window title. It works for the
  current Spotify test case, but Windows Media Session metadata would be more
  robust and app-agnostic.
- Linux implementations for media keys, volume, app volume, and Now Playing are
  still missing.
- `ShellViewModel` (split across `ShellViewModel.cs` and
  `ShellViewModel.Runtime.cs`) is the app root and still the largest coordinator:
  it owns connection flow, config lifecycle, display-trigger editing/runtime,
  event logging, audio sessions, OLED/default display settings, Now Playing
  orchestration, navigation, and the Pad composition. Section view models
  (`PadViewModel`) and the reused editor view models keep most editor state out
  of it, but the runtime orchestration could still move into a dedicated app
  service if it keeps growing.
- Firmware is still a single Arduino sketch. That is fine for prototyping, but
  display/protocol/input modules should be split once behavior stabilizes.
- Layout screens currently support only fixed built-in layouts. A free element
  list or custom layout mode is still open.
- Slider quality is still being validated with replacement hardware. Firmware
  filtering has been improved, but the latest filter still needs a flash and
  real-slider test; add a 100 nF wiper-to-GND capacitor if jitter remains.
- HID fallback needs a flash test with the Arduino ESP32 USB mode set for
  composite CDC + HID. Slider 1 is relative system volume only and does not
  mirror the physical slider position; active-app and "all except active app"
  volume still require the companion app.
- App UI is functional after the display-editor redesign and now guards dense
  editor forms against small-window overlap, but the new Display tab still
  needs hardware-driven usability feedback.

## Verification Baseline

Use these commands after app/core/action changes:

```powershell
dotnet build app\MakroPad.sln
dotnet test app\MakroPad.sln
```

Firmware changes still need Arduino IDE compile/upload and a hardware test on
the ESP32-S3-DevKitC-1.

## Recommended Next Steps

1. Continue refactoring `MainWindowViewModel` only where it still removes a
   real responsibility. Next best target: small device/simulator command
   grouping or an app coordinator if more runtime orchestration is added.
2. Hardware-test the unified Display editor flow against the OLED and adjust
   field grouping/order based on real authoring pain.
3. Add a `custom` layout or free element list if the built-in layouts are too
   restrictive after hardware testing.
4. Replace Spotify-window-title Now Playing with Windows Media Session metadata.
5. Improve the config editor UX with action definitions shared between UI,
   validation, and future docs generation.
6. Split the firmware sketch into protocol, display, buttons, and sliders once
   the new sliders have been tested.

Recommendation: pause broad refactoring unless a new feature touches the
remaining root orchestration. Most editor state and background loop ownership
are now out of `MainWindowViewModel`; future extraction should be driven by
concrete changes.

## Graphify Notes

The graph artifacts are committed under `graphify-out/`:

- `graphify-out/graph.json`
- `graphify-out/GRAPH_REPORT.md`
- `graphify-out/GRAPH_TREE.html`

Useful graph entry points:

- `ShellViewModel` is the biggest god node (app root, split across
  `ShellViewModel.cs` and `ShellViewModel.Runtime.cs`).
- `SerialMakroPadConnection`, `MakroPad Firmware`, and `ActionExecutor` are the
  next most important implementation hubs.
- The most useful architecture question right now is: how does an input event
  flow from firmware through serial protocol into the app action pipeline?
