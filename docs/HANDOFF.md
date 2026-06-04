# MakroPad Handoff

Last updated: 2026-06-03

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
  `EventLogService`. `MainWindowViewModel` is now primarily the UI/application
  coordinator.
- Added `MakroPad.App.Tests` to cover app services and extracted view models,
  including display-trigger orchestration, config lifecycle, action feedback,
  device-session state, and editor validation.
- Added a configurable app-side deadband for `system.volume` and `app.volume`
  slider actions to suppress adjacent-percent jitter. The default is 2
  percentage points and `0` disables the filter.
- Added configurable OLED display triggers for button and slider inputs. The
  app can render normal text or lightweight HTML-like content to OLED text lines
  and sends them as temporary `display;mode=template` messages.

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
- `MainWindowViewModel` is still the largest app coordinator, but connection
  flow, config lifecycle, action helper UI, display-trigger editing/runtime,
  event logging, audio sessions, OLED/default display settings, Now Playing
  orchestration, and device config/OLED sync are now split out. The remaining
  useful decomposition targets are startup/lifetime wiring, command groups for
  simulator/device actions, and possibly a thin app coordinator service if the
  remaining root orchestration keeps growing.
- Firmware is still a single Arduino sketch. That is fine for prototyping, but
  display/protocol/input modules should be split once behavior stabilizes.
- Slider quality is still being validated with replacement hardware.
- App UI is functional after the first redesign, but not yet a polished final
  editor experience.

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
   real responsibility. Next best target: startup/lifetime wiring for background
   loops and async initialization.
2. Replace Spotify-window-title Now Playing with Windows Media Session metadata.
3. Improve the config editor UX with action definitions shared between UI,
   validation, and future docs generation.
4. Split the firmware sketch into protocol, display, buttons, and sliders once
   the new sliders have been tested.
5. Add a small diagnostics screen for current device config, last heartbeat,
   last input, and last OLED message.

Recommendation: extract startup/lifetime wiring next. Most editor state is now
out of `MainWindowViewModel`; the constructor still owns background loop startup
and async initialization, which is the clearest remaining responsibility.

## Graphify Notes

The graph artifacts are committed under `graphify-out/`:

- `graphify-out/graph.json`
- `graphify-out/GRAPH_REPORT.md`
- `graphify-out/GRAPH_TREE.html`

Useful graph entry points:

- `MainWindowViewModel` is the biggest god node.
- `SerialMakroPadConnection`, `MakroPad Firmware`, and `ActionExecutor` are the
  next most important implementation hubs.
- The most useful architecture question right now is: how does an input event
  flow from firmware through serial protocol into the app action pipeline?
