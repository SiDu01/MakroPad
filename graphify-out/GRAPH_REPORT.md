# Graph Report - .  (2026-06-02)

## Corpus Check
- Corpus is ~15,505 words - fits in a single context window. You may not need a graph.

## Summary
- 466 nodes · 632 edges · 62 communities (39 shown, 23 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 8 edges (avg confidence: 0.82)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
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
- [[_COMMUNITY_Community 31|Community 31]]
- [[_COMMUNITY_Community 32|Community 32]]
- [[_COMMUNITY_Community 33|Community 33]]
- [[_COMMUNITY_Community 34|Community 34]]
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
- [[_COMMUNITY_Community 47|Community 47]]

## God Nodes (most connected - your core abstractions)
1. `MainWindowViewModel` - 89 edges
2. `SerialMakroPadConnection` - 23 edges
3. `MakroPad Firmware` - 19 edges
4. `ActionExecutor` - 14 edges
5. `WindowsSystemMediaController` - 14 edges
6. `SimulatedMakroPadConnection` - 13 edges
7. `WindowsKeyboardController` - 11 edges
8. `NoopSystemMediaController` - 10 edges
9. `ActionExecutorTests` - 10 edges
10. `FakeMediaController` - 10 edges

## Surprising Connections (you probably didn't know these)
- `MakroPad Firmware` --semantically_similar_to--> `ESP32-S3 Firmware`  [INFERRED] [semantically similar]
  firmware/MakroPadFirmware/README.md → README.md
- `ActiveSliderCount` --rationale_for--> `slider Message`  [INFERRED]
  firmware/MakroPadFirmware/README.md → docs/PROTOCOL.md
- `Supported Actions Catalog` --conceptually_related_to--> `Action Pipeline`  [EXTRACTED]
  TODO.md → app/README.md
- `action-result Message` --shares_data_with--> `Action Pipeline`  [EXTRACTED]
  docs/PROTOCOL.md → app/README.md
- `Dual Serial Output` --rationale_for--> `USB CDC Serial Transport`  [INFERRED]
  firmware/MakroPadFirmware/README.md → docs/PROTOCOL.md

## Hyperedges (group relationships)
- **Serial Handshake Flow** — protocol_hello_message, protocol_hello_ack_message, app_readme_serial_connection, firmware_readme_makropad_firmware [EXTRACTED 1.00]
- **Input to Action Flow** — hardware_buttons_gpio_pullup, hardware_sliders_adc1, protocol_button_message, protocol_slider_message, app_readme_action_pipeline [EXTRACTED 1.00]
- **OLED Display Configuration Flow** — hardware_oled_i2c_ssd1306, protocol_display_message, protocol_config_message, protocol_action_result_message, app_readme_oled_default_screen [EXTRACTED 1.00]

## Communities (62 total, 23 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.08
Nodes (38): Action Pipeline, MakroPad.Actions Project, MakroPad App, MakroPad.App Project, MakroPad.Core Project, MakroPad.Device Project, OLED Default Screen, Serial Connection (+30 more)

### Community 1 - "Community 1"
Cohesion: 0.07
Nodes (19): ActionPipeline, BindingViewModel, IReadOnlyDictionary, IReadOnlyList, bool, CancellationTokenSource, IApplicationVolumeController, IMakroPadConnection (+11 more)

### Community 2 - "Community 2"
Cohesion: 0.08
Nodes (7): IKeyboardController, ISystemLauncher, NoopKeyboardController, SystemLauncher, ActionExecutorTests, FakeKeyboardController, FakeSystemLauncher

### Community 3 - "Community 3"
Cohesion: 0.16
Nodes (10): DateTimeOffset, IDisposable, CancellationTokenSource, DeviceConnectionState, int, TimeSpan, SerialMakroPadConnection, object (+2 more)

### Community 4 - "Community 4"
Cohesion: 0.11
Nodes (22): Build and Test Workflow, German Collaboration Rules, Git Commit Workflow, MakroPad Agent Workflow, Repository Editing Rules, Dual Serial Output, Firmware and Desktop App Architecture, Avalonia UI Technology Choice (+14 more)

### Community 5 - "Community 5"
Cohesion: 0.11
Nodes (6): ISystemVolumeController, NoopSystemVolumeController, FakeVolumeController, ActionPipelineTests, FakeVolumeController, WindowsSystemVolumeController

### Community 6 - "Community 6"
Cohesion: 0.20
Nodes (3): ISystemMediaController, FakeMediaController, FakeMediaController

### Community 7 - "Community 7"
Cohesion: 0.24
Nodes (6): ActionExecutor, IApplicationVolumeController, IKeyboardController, ISystemLauncher, ISystemMediaController, ISystemVolumeController

### Community 8 - "Community 8"
Cohesion: 0.18
Nodes (4): IApplicationVolumeController, NoopApplicationVolumeController, FakeApplicationVolumeController, WindowsApplicationVolumeController

### Community 9 - "Community 9"
Cohesion: 0.24
Nodes (3): uint, ushort, WindowsSystemMediaController

### Community 10 - "Community 10"
Cohesion: 0.23
Nodes (3): IMakroPadConnection, DeviceConnectionState, SimulatedMakroPadConnection

### Community 12 - "Community 12"
Cohesion: 0.27
Nodes (4): Dictionary, uint, ushort, WindowsKeyboardController

### Community 14 - "Community 14"
Cohesion: 0.24
Nodes (5): Application, MainWindow, App, bool, TrayIcon

### Community 19 - "Community 19"
Cohesion: 0.29
Nodes (5): ActionExecutor, ActionResolver, ActionPipeline, Dictionary, TimeSpan

### Community 26 - "Community 26"
Cohesion: 0.40
Nodes (3): INowPlayingProvider, string, ProcessWindowNowPlayingProvider

### Community 44 - "Community 44"
Cohesion: 1.00
Nodes (3): ActionDefinition Metadata Model, Config Editor Parameter Hints, Structured Action Controls

## Knowledge Gaps
- **71 isolated node(s):** `ISystemMediaController`, `ISystemVolumeController`, `IApplicationVolumeController`, `IKeyboardController`, `ISystemLauncher` (+66 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **23 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.