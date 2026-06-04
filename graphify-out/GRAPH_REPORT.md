# Graph Report - MakroPad  (2026-06-04)

## Corpus Check
- 126 files · ~30,004 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1280 nodes · 1864 edges · 145 communities (89 shown, 56 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 13 edges (avg confidence: 0.81)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `d38d4fcb`
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
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]
- [[_COMMUNITY_Community 25|Community 25]]
- [[_COMMUNITY_Community 26|Community 26]]
- [[_COMMUNITY_Community 27|Community 27]]
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
- [[_COMMUNITY_Community 88|Community 88]]
- [[_COMMUNITY_Community 89|Community 89]]
- [[_COMMUNITY_Community 90|Community 90]]
- [[_COMMUNITY_Community 91|Community 91]]
- [[_COMMUNITY_Community 92|Community 92]]
- [[_COMMUNITY_Community 93|Community 93]]
- [[_COMMUNITY_Community 95|Community 95]]
- [[_COMMUNITY_Community 97|Community 97]]
- [[_COMMUNITY_Community 98|Community 98]]
- [[_COMMUNITY_Community 99|Community 99]]
- [[_COMMUNITY_Community 100|Community 100]]
- [[_COMMUNITY_Community 101|Community 101]]
- [[_COMMUNITY_Community 103|Community 103]]
- [[_COMMUNITY_Community 104|Community 104]]
- [[_COMMUNITY_Community 105|Community 105]]
- [[_COMMUNITY_Community 106|Community 106]]
- [[_COMMUNITY_Community 107|Community 107]]
- [[_COMMUNITY_Community 108|Community 108]]
- [[_COMMUNITY_Community 109|Community 109]]
- [[_COMMUNITY_Community 110|Community 110]]
- [[_COMMUNITY_Community 111|Community 111]]
- [[_COMMUNITY_Community 112|Community 112]]
- [[_COMMUNITY_Community 113|Community 113]]
- [[_COMMUNITY_Community 114|Community 114]]
- [[_COMMUNITY_Community 124|Community 124]]
- [[_COMMUNITY_Community 125|Community 125]]
- [[_COMMUNITY_Community 126|Community 126]]
- [[_COMMUNITY_Community 127|Community 127]]
- [[_COMMUNITY_Community 128|Community 128]]
- [[_COMMUNITY_Community 129|Community 129]]
- [[_COMMUNITY_Community 130|Community 130]]
- [[_COMMUNITY_Community 131|Community 131]]
- [[_COMMUNITY_Community 132|Community 132]]
- [[_COMMUNITY_Community 133|Community 133]]
- [[_COMMUNITY_Community 134|Community 134]]
- [[_COMMUNITY_Community 135|Community 135]]
- [[_COMMUNITY_Community 136|Community 136]]
- [[_COMMUNITY_Community 137|Community 137]]
- [[_COMMUNITY_Community 138|Community 138]]
- [[_COMMUNITY_Community 139|Community 139]]
- [[_COMMUNITY_Community 140|Community 140]]
- [[_COMMUNITY_Community 141|Community 141]]
- [[_COMMUNITY_Community 142|Community 142]]
- [[_COMMUNITY_Community 143|Community 143]]
- [[_COMMUNITY_Community 144|Community 144]]

## God Nodes (most connected - your core abstractions)
1. `MainWindowViewModel` - 200 edges
2. `DisplayTriggersEditorViewModel` - 61 edges
3. `SerialMakroPadConnection` - 30 edges
4. `ProfilesEditorViewModel` - 26 edges
5. `DisplaySettingsViewModel` - 25 edges
6. `ProfileConfigServiceTests` - 24 edges
7. `DisplayTriggerOrchestrator` - 23 edges
8. `ActionExecutor` - 20 edges
9. `ActionEditorViewModel` - 20 edges
10. `MakroPad Firmware` - 19 edges

## Surprising Connections (you probably didn't know these)
- `MakroPad Firmware` --semantically_similar_to--> `ESP32-S3 Firmware`  [INFERRED] [semantically similar]
  firmware/MakroPadFirmware/README.md → README.md
- `ActiveSliderCount` --rationale_for--> `slider Message`  [INFERRED]
  firmware/MakroPadFirmware/README.md → docs/PROTOCOL.md
- `MakroPad Firmware` --implements--> `action-result Message`  [EXTRACTED]
  firmware/MakroPadFirmware/README.md → docs/PROTOCOL.md
- `ActionExecutor` --references--> `ISystemMediaController`  [EXTRACTED]
  app/MakroPad.Actions/ActionExecutor.cs → MakroPad.Actions/ActionExecutor.cs
- `ActionExecutor` --references--> `ISystemVolumeController`  [EXTRACTED]
  app/MakroPad.Actions/ActionExecutor.cs → MakroPad.Actions/ActionExecutor.cs

## Hyperedges (group relationships)
- **Serial Handshake Flow** — protocol_hello_message, protocol_hello_ack_message, app_readme_serial_connection, firmware_readme_makropad_firmware [EXTRACTED 1.00]
- **Input to Action Flow** — hardware_buttons_gpio_pullup, hardware_sliders_adc1, protocol_button_message, protocol_slider_message, app_readme_action_pipeline [EXTRACTED 1.00]
- **OLED Display Configuration Flow** — hardware_oled_i2c_ssd1306, protocol_display_message, protocol_config_message, protocol_action_result_message, app_readme_oled_default_screen [EXTRACTED 1.00]

## Communities (145 total, 56 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.18
Nodes (19): OLED Default Screen, ActiveSliderCount, Adafruit GFX Library, Adafruit SSD1306 Library, MakroPad Firmware, Wire Library, Buttons GPIO INPUT_PULLUP Wiring, ESP32-S3-DevKitC-1 (+11 more)

### Community 1 - "Community 1"
Cohesion: 0.03
Nodes (37): ActionFeedbackCoordinator, ActionPipeline, ActionDefinitionCatalog, ActionPipeline, BindingEditorService, bool, CancellationTokenSource, DeviceDisplaySyncService (+29 more)

### Community 3 - "Community 3"
Cohesion: 0.06
Nodes (19): CancellationTokenSource, DeviceConnectionState, int, object, TimeSpan, DeviceConnectionState, DateTimeOffset, IMakroPadConnection (+11 more)

### Community 4 - "Community 4"
Cohesion: 0.25
Nodes (11): Build and Test Workflow, Firmware and Desktop App Architecture, Avalonia UI Technology Choice, Hardware Layout Configuration, USB HID Role, User Profile Configuration, Desktop Companion App, ESP32-S3 Firmware (+3 more)

### Community 5 - "Community 5"
Cohesion: 0.17
Nodes (3): ActionPipelineTests, FakeMediaController, FakeVolumeController

### Community 6 - "Community 6"
Cohesion: 0.05
Nodes (40): action-result, App -> ESP Nachrichten, Beispielablauf, button, code:text (type;key=value;key=value), code:text (button;id=1;state=down), code:text (slider;id=1;value=1834;raw=1841), code:text (error;code=display_init_failed;message=OLED%20not%20found) (+32 more)

### Community 7 - "Community 7"
Cohesion: 0.15
Nodes (11): IApplicationVolumeController, IKeyboardController, ISystemLauncher, ISystemMediaController, ISystemVolumeController, ActionExecutor, IApplicationVolumeController, IKeyboardController (+3 more)

### Community 9 - "Community 9"
Cohesion: 0.11
Nodes (7): uint, ushort, ISystemMediaController, NoopSystemMediaController, uint, ushort, WindowsSystemMediaController

### Community 11 - "Community 11"
Cohesion: 0.20
Nodes (3): IApplicationVolumeController, NoopApplicationVolumeController, WindowsApplicationVolumeController

### Community 12 - "Community 12"
Cohesion: 0.18
Nodes (7): Dictionary, uint, ushort, Dictionary, uint, ushort, WindowsKeyboardController

### Community 14 - "Community 14"
Cohesion: 0.20
Nodes (6): bool, Application, MainWindow, App, bool, TrayIcon

### Community 18 - "Community 18"
Cohesion: 0.15
Nodes (8): bool, CancellationTokenSource, DisplayTriggerInputTracker, IDisplayTriggerSink, object, DisplayTriggerInputTracker, IDisposable, DisplayTriggerOrchestrator

### Community 19 - "Community 19"
Cohesion: 0.17
Nodes (8): ActionExecutor, ActionResolver, Dictionary, int, TimeSpan, ActionPipeline, Dictionary, TimeSpan

### Community 21 - "Community 21"
Cohesion: 0.16
Nodes (10): BindingEditorService, bool, MakroPadConfig, ProfileConfigService, string, BindingViewModel, EventArgs, DisplayTriggerTestRequestedEventArgs (+2 more)

### Community 22 - "Community 22"
Cohesion: 0.07
Nodes (13): DeviceDisplaySyncService, object, DisplayTriggerRuntimeState, Func, IDisplayTriggerSink, ISystemLauncher, SystemLauncher, ActionFeedbackCoordinatorTests (+5 more)

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
Cohesion: 0.07
Nodes (13): ActionDefinitionCatalog, BindingEditorService, bool, string, IApplicationVolumeController, string, INowPlayingProvider, string (+5 more)

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

### Community 70 - "Community 70"
Cohesion: 0.33
Nodes (5): Config Editor Parameter Hints, Handoff, Now Playing, Technical Debt, TODO

### Community 71 - "Community 71"
Cohesion: 0.50
Nodes (3): code:powershell (dotnet build app\MakroPad.sln), Current Status, MakroPad

### Community 90 - "Community 90"
Cohesion: 0.25
Nodes (4): ISystemVolumeController, DeviceConfigMessageBuilder, OledDefaultScreenRenderer, DeviceDisplaySyncService

### Community 92 - "Community 92"
Cohesion: 0.14
Nodes (6): bool, DeviceDisplaySyncService, int, string, NowPlayingTrack, DisplaySettingsViewModel

### Community 98 - "Community 98"
Cohesion: 0.33
Nodes (3): Dictionary, object, DisplayTriggerInputTracker

### Community 100 - "Community 100"
Cohesion: 0.14
Nodes (6): SemaphoreSlim, string, MakroPadDeviceProbe, SerialMakroPadConnection, DeviceSessionService, SimulatedMakroPadConnection

### Community 105 - "Community 105"
Cohesion: 0.39
Nodes (4): ActionDefinitionCatalog, BindingEditorService, Failed(), Succeeded()

### Community 107 - "Community 107"
Cohesion: 0.33
Nodes (4): SemaphoreSlim, MakroPadConfigStore, SemaphoreSlim, MakroPadConfigService

### Community 108 - "Community 108"
Cohesion: 0.40
Nodes (3): IReadOnlyList, IReadOnlyDictionary, ActionDefinitionCatalog

### Community 124 - "Community 124"
Cohesion: 0.07
Nodes (8): bool, int, IReadOnlyList, string, DisplayTriggerPreset, DisplayTriggerViewModel, HashSet, DisplayTriggersEditorViewModel

### Community 125 - "Community 125"
Cohesion: 0.22
Nodes (4): IKeyboardController, FakeApplicationVolumeController, FakeKeyboardController, FakeSystemLauncher

### Community 126 - "Community 126"
Cohesion: 0.12
Nodes (15): Build, code:text (%APPDATA%\MakroPad\config.json), code:text (hello-ack;proto=1;app=0.1.0), code:powershell (dotnet run --project app\MakroPad.App\MakroPad.App.csproj), code:powershell (dotnet build app\MakroPad.sln), code:powershell (dotnet test app\MakroPad.sln), Current Prototype, MakroPad App (+7 more)

### Community 130 - "Community 130"
Cohesion: 0.33
Nodes (4): ActionPipeline, DeviceSessionService, DisplayTriggerOrchestrator, ActionFeedbackCoordinator

### Community 137 - "Community 137"
Cohesion: 0.25
Nodes (3): ISystemVolumeController, FakeVolumeController, FakeSystemVolumeController

### Community 138 - "Community 138"
Cohesion: 0.29
Nodes (7): Dual Serial Output, USB CDC Serial Role, error Message, JSON Lines Alternative, Version 1 Key-Value Protocol Format, Protocol Percent-Encoding, USB CDC Serial Transport

### Community 139 - "Community 139"
Cohesion: 0.33
Nodes (6): Action Pipeline, MakroPad.Actions Project, Windows Media Actions, Windows Volume Actions, action-result Message, Supported Actions Catalog

### Community 141 - "Community 141"
Cohesion: 0.50
Nodes (5): MakroPad.Device Project, Serial Connection, Simulator Connection, hello-ack Message, hello Message

### Community 142 - "Community 142"
Cohesion: 0.50
Nodes (4): German Collaboration Rules, Git Commit Workflow, MakroPad Agent Workflow, Repository Editing Rules

### Community 144 - "Community 144"
Cohesion: 0.67
Nodes (4): Serial Heartbeat, Waiting for app Display State, ping Message, pong Message

## Knowledge Gaps
- **247 isolated node(s):** `PreToolUse`, `ISystemMediaController`, `ISystemVolumeController`, `IApplicationVolumeController`, `IKeyboardController` (+242 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **56 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainWindowViewModel` connect `Community 1` to `Community 2`, `Community 100`, `Community 102`, `Community 107`, `Community 108`, `Community 45`, `Community 13`, `Community 92`, `Community 16`, `Community 18`, `Community 21`, `Community 90`, `Community 124`, `Community 62`?**
  _High betweenness centrality (0.101) - this node is a cross-community bridge._
- **Why does `DisplayTriggersEditorViewModel` connect `Community 124` to `Community 45`, `Community 21`?**
  _High betweenness centrality (0.044) - this node is a cross-community bridge._
- **Why does `ViewModelBase` connect `Community 45` to `Community 124`, `Community 1`, `Community 92`, `Community 21`?**
  _High betweenness centrality (0.043) - this node is a cross-community bridge._
- **What connects `PreToolUse`, `ISystemMediaController`, `ISystemVolumeController` to the rest of the system?**
  _247 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.033950617283950615 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.1066066066066066 - nodes in this community are weakly interconnected._
- **Should `Community 3` be split into smaller, more focused modules?**
  _Cohesion score 0.05957767722473605 - nodes in this community are weakly interconnected._