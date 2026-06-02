# Graph Report - MakroPad  (2026-06-02)

## Corpus Check
- 75 files · ~18,981 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 756 nodes · 973 edges · 89 communities (64 shown, 25 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 12 edges (avg confidence: 0.81)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `bb26f076`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]
- [[_COMMUNITY_Community 25|Community 25]]
- [[_COMMUNITY_Community 26|Community 26]]
- [[_COMMUNITY_Community 28|Community 28]]
- [[_COMMUNITY_Community 29|Community 29]]
- [[_COMMUNITY_Community 30|Community 30]]
- [[_COMMUNITY_Community 31|Community 31]]
- [[_COMMUNITY_Community 32|Community 32]]
- [[_COMMUNITY_Community 33|Community 33]]
- [[_COMMUNITY_Community 34|Community 34]]
- [[_COMMUNITY_Community 35|Community 35]]
- [[_COMMUNITY_Community 36|Community 36]]
- [[_COMMUNITY_Community 37|Community 37]]
- [[_COMMUNITY_Community 38|Community 38]]
- [[_COMMUNITY_Community 39|Community 39]]
- [[_COMMUNITY_Community 40|Community 40]]
- [[_COMMUNITY_Community 41|Community 41]]
- [[_COMMUNITY_Community 42|Community 42]]
- [[_COMMUNITY_Community 43|Community 43]]
- [[_COMMUNITY_Community 44|Community 44]]
- [[_COMMUNITY_Community 45|Community 45]]
- [[_COMMUNITY_Community 46|Community 46]]
- [[_COMMUNITY_Community 47|Community 47]]
- [[_COMMUNITY_Community 48|Community 48]]
- [[_COMMUNITY_Community 62|Community 62]]
- [[_COMMUNITY_Community 63|Community 63]]
- [[_COMMUNITY_Community 64|Community 64]]
- [[_COMMUNITY_Community 65|Community 65]]
- [[_COMMUNITY_Community 66|Community 66]]
- [[_COMMUNITY_Community 67|Community 67]]
- [[_COMMUNITY_Community 68|Community 68]]
- [[_COMMUNITY_Community 69|Community 69]]
- [[_COMMUNITY_Community 70|Community 70]]
- [[_COMMUNITY_Community 71|Community 71]]
- [[_COMMUNITY_Community 72|Community 72]]
- [[_COMMUNITY_Community 90|Community 90]]

## God Nodes (most connected - your core abstractions)
1. `MainWindowViewModel` - 125 edges
2. `SerialMakroPadConnection` - 28 edges
3. `ActionExecutor` - 20 edges
4. `MakroPad Firmware` - 19 edges
5. `WindowsSystemMediaController` - 17 edges
6. `WindowsKeyboardController` - 15 edges
7. `SimulatedMakroPadConnection` - 15 edges
8. `ActionPipeline` - 14 edges
9. `Serial Protocol` - 12 edges
10. `NoopSystemMediaController` - 11 edges

## Surprising Connections (you probably didn't know these)
- `MakroPad Firmware` --semantically_similar_to--> `ESP32-S3 Firmware`  [INFERRED] [semantically similar]
  firmware/MakroPadFirmware/README.md → README.md
- `ActiveSliderCount` --rationale_for--> `slider Message`  [INFERRED]
  firmware/MakroPadFirmware/README.md → docs/PROTOCOL.md
- `ActionExecutor` --references--> `ISystemMediaController`  [EXTRACTED]
  app/MakroPad.Actions/ActionExecutor.cs → MakroPad.Actions/ActionExecutor.cs
- `ActionExecutor` --references--> `ISystemVolumeController`  [EXTRACTED]
  app/MakroPad.Actions/ActionExecutor.cs → MakroPad.Actions/ActionExecutor.cs
- `ActionExecutor` --references--> `IApplicationVolumeController`  [EXTRACTED]
  app/MakroPad.Actions/ActionExecutor.cs → MakroPad.Actions/ActionExecutor.cs

## Hyperedges (group relationships)
- **Serial Handshake Flow** — protocol_hello_message, protocol_hello_ack_message, app_readme_serial_connection, firmware_readme_makropad_firmware [EXTRACTED 1.00]
- **Input to Action Flow** — hardware_buttons_gpio_pullup, hardware_sliders_adc1, protocol_button_message, protocol_slider_message, app_readme_action_pipeline [EXTRACTED 1.00]
- **OLED Display Configuration Flow** — hardware_oled_i2c_ssd1306, protocol_display_message, protocol_config_message, protocol_action_result_message, app_readme_oled_default_screen [EXTRACTED 1.00]

## Communities (89 total, 25 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.11
Nodes (29): Action Pipeline, MakroPad.Actions Project, OLED Default Screen, Serial Heartbeat, Windows Media Actions, Windows Volume Actions, ActiveSliderCount, Adafruit GFX Library (+21 more)

### Community 1 - "Community 1"
Cohesion: 0.05
Nodes (29): ActionPipeline, bool, CancellationTokenSource, Dictionary, IMakroPadConnection, INowPlayingProvider, int, ISystemVolumeController (+21 more)

### Community 3 - "Community 3"
Cohesion: 0.12
Nodes (14): CancellationTokenSource, DeviceConnectionState, int, TimeSpan, DateTimeOffset, IDisposable, CancellationTokenSource, DeviceConnectionState (+6 more)

### Community 4 - "Community 4"
Cohesion: 0.11
Nodes (22): Build and Test Workflow, German Collaboration Rules, Git Commit Workflow, MakroPad Agent Workflow, Repository Editing Rules, Dual Serial Output, Firmware and Desktop App Architecture, Avalonia UI Technology Choice (+14 more)

### Community 5 - "Community 5"
Cohesion: 0.10
Nodes (5): ISystemMediaController, NoopSystemMediaController, ActionPipelineTests, FakeMediaController, FakeVolumeController

### Community 6 - "Community 6"
Cohesion: 0.05
Nodes (40): action-result, App -> ESP Nachrichten, Beispielablauf, button, code:text (type;key=value;key=value), code:text (button;id=1;state=down), code:text (slider;id=1;value=1834;raw=1841), code:text (error;code=display_init_failed;message=OLED%20not%20found) (+32 more)

### Community 7 - "Community 7"
Cohesion: 0.15
Nodes (11): IApplicationVolumeController, IKeyboardController, ISystemLauncher, ISystemMediaController, ISystemVolumeController, ActionExecutor, IApplicationVolumeController, IKeyboardController (+3 more)

### Community 8 - "Community 8"
Cohesion: 0.15
Nodes (4): IApplicationVolumeController, NoopApplicationVolumeController, FakeApplicationVolumeController, WindowsApplicationVolumeController

### Community 9 - "Community 9"
Cohesion: 0.18
Nodes (5): uint, ushort, uint, ushort, WindowsSystemMediaController

### Community 10 - "Community 10"
Cohesion: 0.19
Nodes (4): DeviceConnectionState, IMakroPadConnection, DeviceConnectionState, SimulatedMakroPadConnection

### Community 12 - "Community 12"
Cohesion: 0.13
Nodes (9): Dictionary, uint, ushort, IKeyboardController, NoopKeyboardController, Dictionary, uint, ushort (+1 more)

### Community 14 - "Community 14"
Cohesion: 0.20
Nodes (6): bool, Application, MainWindow, App, bool, TrayIcon

### Community 19 - "Community 19"
Cohesion: 0.17
Nodes (8): ActionExecutor, ActionResolver, Dictionary, int, TimeSpan, ActionPipeline, Dictionary, TimeSpan

### Community 23 - "Community 23"
Cohesion: 0.31
Nodes (3): int, int, MakroPadDeviceProbe

### Community 26 - "Community 26"
Cohesion: 0.29
Nodes (4): string, INowPlayingProvider, string, ProcessWindowNowPlayingProvider

### Community 30 - "Community 30"
Cohesion: 0.60
Nodes (3): Executed(), Throttled(), Unbound()

### Community 44 - "Community 44"
Cohesion: 1.00
Nodes (3): ActionDefinition Metadata Model, Config Editor Parameter Hints, Structured Action Controls

### Community 45 - "Community 45"
Cohesion: 0.14
Nodes (8): IApplicationVolumeController, string, INowPlayingProvider, string, ObservableObject, AudioSessionsViewModel, NowPlayingViewModel, ViewModelBase

### Community 62 - "Community 62"
Cohesion: 0.10
Nodes (20): Build, code:text (%APPDATA%\MakroPad\config.json), code:text (hello-ack;proto=1;app=0.1.0), code:powershell (dotnet run --project app\MakroPad.App\MakroPad.App.csproj), code:powershell (dotnet build app\MakroPad.sln), code:powershell (dotnet test app\MakroPad.sln), Current Prototype, MakroPad App (+12 more)

### Community 63 - "Community 63"
Cohesion: 0.11
Nodes (18): Board-Revision pruefen, Buttons, Buttons, code:text (GPIO ---- Switch ---- GND), code:text (3V3 ---- Potentiometer Ende 1), code:text (OLED VCC -> 3V3), Elektrisches Grundprinzip, Erster Prototyp (+10 more)

### Community 64 - "Community 64"
Cohesion: 0.12
Nodes (16): Bekannte Anforderungen, code:text (hello;fw=0.1.0;device=makropad-s3), code:text (display;mode=media;artist=Daft Punk;title=Digital Love), code:json ({), code:text (MakroPad/), Desktop Companion App, Empfohlene Architektur, Empfohlener erster Meilenstein (+8 more)

### Community 65 - "Community 65"
Cohesion: 0.13
Nodes (14): code:text (hello;proto=1;fw=0.2.0;device=makropad-s3;buttons=20;sliders), code:text (hello-ack;proto=1;app=manual), code:text (ping;id=1), code:text (pong;id=1), code:text (display;mode=message;text=Hello%20MakroPad), code:text (config;profile=Default;actionResultTimeoutMs=3000;preferredD), code:text (button;id=1;state=down), code:text (button;id=12;state=down) (+6 more)

### Community 66 - "Community 66"
Cohesion: 0.18
Nodes (10): code:powershell (dotnet build app\MakroPad.sln), Current State, Graphify Notes, Known Limitations, MakroPad Handoff, OLED Behavior, Recent Milestones, Recommended Next Steps (+2 more)

### Community 67 - "Community 67"
Cohesion: 0.20
Nodes (9): Abschlussformat, code:powershell (dotnet build app\MakroPad.sln), Entwicklung, Git, graphify, MakroPad Agent Workflow, Projektfokus, Sprache und Zusammenarbeit (+1 more)

### Community 69 - "Community 69"
Cohesion: 0.05
Nodes (10): ISystemLauncher, ISystemVolumeController, NoopSystemVolumeController, SystemLauncher, ActionExecutorTests, FakeKeyboardController, FakeMediaController, FakeSystemLauncher (+2 more)

### Community 70 - "Community 70"
Cohesion: 0.33
Nodes (5): Config Editor Parameter Hints, Handoff, Now Playing, Technical Debt, TODO

### Community 71 - "Community 71"
Cohesion: 0.50
Nodes (3): code:powershell (dotnet build app\MakroPad.sln), Current Status, MakroPad

### Community 90 - "Community 90"
Cohesion: 0.29
Nodes (3): ISystemVolumeController, OledDefaultScreenRenderer, DeviceDisplaySyncService

## Knowledge Gaps
- **193 isolated node(s):** `PreToolUse`, `ISystemMediaController`, `ISystemVolumeController`, `IApplicationVolumeController`, `IKeyboardController` (+188 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **25 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainWindowViewModel` connect `Community 1` to `Community 2`, `Community 11`, `Community 13`, `Community 45`, `Community 16`, `Community 18`, `Community 90`, `Community 27`?**
  _High betweenness centrality (0.036) - this node is a cross-community bridge._
- **Why does `FakeMediaController` connect `Community 69` to `Community 5`?**
  _High betweenness centrality (0.013) - this node is a cross-community bridge._
- **Why does `FakeKeyboardController` connect `Community 69` to `Community 12`?**
  _High betweenness centrality (0.008) - this node is a cross-community bridge._
- **What connects `PreToolUse`, `ISystemMediaController`, `ISystemVolumeController` to the rest of the system?**
  _193 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.11083743842364532 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.0473469387755102 - nodes in this community are weakly interconnected._
- **Should `Community 3` be split into smaller, more focused modules?**
  _Cohesion score 0.12169312169312169 - nodes in this community are weakly interconnected._