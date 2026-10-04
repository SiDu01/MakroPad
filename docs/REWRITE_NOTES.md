# Neuentwicklung – Bestandsaufnahme

Stand: 2026-10-04. Grundlage: App 0.2.0, Firmware 0.2.0, Gehäuse in `hardware/case`.

Ziel dieser Notiz: festhalten, welche Funktionen und Ideen aus dem bisherigen
Projekt in den Neubau gehören, welche vereinfacht werden sollten und welche
wegfallen können.

## 1. Wo der Fokus verloren ging

Der Kern des Geräts ist **Taste/Slider drücken → am PC passiert etwas**. Der
größte Teil von Code und Doku steckt aber im **OLED-Subsystem**:

- `DisplayTriggersEditorViewModel.cs` ist mit ~1070 Zeilen die größte Datei,
  `DisplayView.axaml` (~600) die größte View, `ShellViewModel.Runtime.cs` (~880)
  orchestriert vor allem Display-Zustände.
- Es gibt **vier Inhaltsformate** (Text, HTML-Subset, Markdown-Pseudocode,
  Layout) plus einen visuellen Baukasten – für ein 128×64-Display mit 5 Zeilen.
- Es gibt **sieben Wege**, wie etwas aufs Display kommt: `config` (Idle),
  `display;mode=template`, `mode=layout`, `mode=media`, `mode=profile`,
  `mode=message`, `action-result` (Fallback). Dazu Platzhalter, die teils in der
  App, teils in der Firmware aufgelöst werden.
- Display-Trigger kennen fünf Event-Typen, Idle-Screens haben Bedingungen,
  Priorität, Exklusiv-Modus, Aktivierungsverzögerung, Mindest-Standzeit und
  Rotation – jeweils mit Migrationscode für ältere Config-Formate.
- Die eigentlichen Actions (13 Stück) sind dagegen schlicht geblieben;
  Ideen aus dem Projektplan (Textbausteine, Skripte, HTTP, OBS/Discord,
  Multi-Step-Makros) wurden nie umgesetzt.
- Hardware und Software laufen auseinander: Das Gehäuse hat **16 Tasten**
  (5×4-Raster, 2×2 durch das OLED ersetzt) + 3 Slider, Firmware und App gehen
  von **20 Tasten** aus.

## 2. Kern – übernehmen (Idee und meist auch Logik)

| Funktion | Warum relevant | Wo im alten Code |
| --- | --- | --- |
| Zeilenbasiertes Serial-Protokoll `type;key=value` mit Percent-Encoding | Einfach in Arduino und C#, im Serial Monitor lesbar und tippbar, gut testbar | `MakroPad.Core/Protocol/ProtocolCodec.cs`, `docs/PROTOCOL.md` |
| Handshake `hello`/`hello-ack` mit `proto`-Version und Gerätefähigkeiten (`buttons`, `sliders`, `display`) | App erkennt Gerät + Hardwareumfang automatisch | Firmware `sendHello()`, `DeviceSessionService` |
| Heartbeat `ping`/`pong`, jede gültige Nachricht zählt als Lebenszeichen, Zustand `Unresponsive` | Robuste Verbindungserkennung in beide Richtungen | `SerialMakroPadConnection.HeartbeatLoopAsync`, Firmware `updateHostTimeout()` |
| Auto-Connect: alle COM-Ports nach `hello;device=makropad-s3` abfragen, zuletzt genutzten Port zuerst | Kein manuelles Port-Wählen | `MakroPad.Device/MakroPadDeviceProbe.cs` |
| Simulator-Verbindung hinter `IMakroPadConnection` | Entwicklung und Tests ohne Hardware | `SimulatedMakroPadConnection.cs` |
| Tasten-Entprellung (25 ms, nur Zustandswechsel senden) | Basis der Eingabe | Firmware `pollButton()` |
| **Slider-Filterkette**: 9 Samples → sortieren → mittlere 5 mitteln → Tiefpass (1/16) → Endpunkt-Snap → Deadband (32) → Mindest-Sendeintervall (35 ms); Hardware-Tipp 100 nF am Wiper | Hart erarbeitet, löst das Jitter-Problem | Firmware `readSliderRaw()`/`pollSlider()`, `docs/HARDWARE.md` |
| App-seitige Slider-Drosselung (100 ms) + Prozent-Deadband für Lautstärke-Actions, 0 %/100 % immer durchlassen | Verhindert Audio-API-Spam und Wackeln | `MakroPad.Actions/ActionPipeline.cs` |
| Binding-Modell `Input → Action + Parameter` mit Input-IDs `button:N`/`slider:N` | Einfach, erweiterbar | `ActionBinding.cs`, `ActionResolver.cs` |
| Profile (mehrere Binding-Sets, aktives Profil) | Grundfunktion jedes Makropads | `ProfileConfig`, `ProfileConfigService` |
| Actions: Media-Keys, `system.volume`, `app.volume`, `openUrl`, `openApp`, `keyboard.shortcut` | Bewährter Kern-Umfang | `MakroPad.Actions/*` |
| Per-App-Lautstärke über CoreAudio-Sessions (NAudio), Prozessname normalisiert (`.exe` weg), alle Sessions eines Prozesses | Das Alleinstellungsmerkmal gegenüber reinem HID | `WindowsApplicationVolumeController.cs` |
| Liste aktiver Audio-Sessions (Prozess, Lautstärke, Mute, aktiv) | Auswahlhilfe im Editor statt Prozessnamen tippen | `GetAudioSessionsAsync()` |
| Plattform-Interfaces + Factory + Noop-Implementierung (`IKeyboardController`, `ISystemVolumeController` …) | Saubere Trennung Windows/Linux, testbar | `MakroPad.Actions/*Factory.cs`, `Noop*.cs` |
| `ActionExecutionResult` (ok/failed + Meldung) als Rückgabe jeder Action | Einheitliches Feedback für UI, Log und OLED | `ActionExecutionResult.cs` |
| Action-Metadaten (Name, Parameter, Pflichtfelder, Beispiel, Hinweis) als eine Quelle für Liste, Editor, Validierung, Doku | Richtige Idee, im Neubau von Anfang an | `ActionDefinitionCatalog.cs`, `TODO.md` |
| Shortcut-Recorder und App/Datei-Picker im Editor | Spart Tipparbeit, vermeidet Fehler | `ActionEditorViewModel`, `PadView.axaml.cs` |
| JSON-Config unter `%APPDATA%\MakroPad\config.json`, atomar schreiben (Temp-Datei + Move) | Kein kaputtes Config bei Absturz | `MakroPadConfigStore.SaveAsync()` |
| Tray-Betrieb, Start im Tray, Fenster nur bei Bedarf | Gerät soll im Hintergrund laufen | `App.axaml.cs`, `AppSettingsConfig` |
| **HID-Fallback ohne App**: Tasten 1–7 als Consumer-Control (Play/Pause, Next, Prev, Stop, Mute, Vol±), Slider 1 als relative Lautstärke | Gerät bleibt ohne App nutzbar | Firmware `handleHidFallback*()`, `docs/PROTOCOL.md` |
| Geräte-zentrierte UI: Pad-Abbild mit Tasten-Grid + Slider-Streifen, Klick → Inspector, Live-Highlight beim Drücken, Edit/Test-Modus | Die beste UI-Idee des Projekts | `PadViewModel`, `PadView.axaml` |
| Konfigurierbares Pad-Layout (Zeilen/Spalten, Slider-Anzahl) | Passt UI an reale Hardware an (16 statt 20) | `HardwareConfig`, `PadViewModel` |
| Diagnose: begrenztes Event-Log (Rohzeilen rein/raus) | Unverzichtbar beim Hardware-Debugging | `EventLogService` |
| Unit-Tests für Codec, Pipeline, Config | Gute Basis, Muster übernehmen | `*.Tests` |

## 3. Gute Ideen – stark vereinfacht übernehmen

| Idee | Vereinfachung für den Neubau |
| --- | --- |
| Idle-/Standard-Screen mit Platzhaltern (`{profile}`, `{time}`, `{date}`, `{volume.system}`, `{media.title}` …) | Ein Standard-Screen pro Profil. Platzhalter **nur an einer Stelle** auflösen (App), Firmware zeigt fertigen Text. Ausnahme: Uhr läuft in der Firmware weiter (siehe unten). |
| Firmware-Uhr läuft ohne App weiter (letzte Host-Zeit + `millis()`) | Behalten, kleiner Aufwand, großer Nutzen. |
| Temporäre Feedback-Screens (Lautstärke mit Balken, Action ok/fehlgeschlagen, Profilwechsel, Track-Wechsel) | Fest eingebaute Screens mit wenigen Feldern statt frei konfigurierbarer Trigger. Höchstens: pro Ereignis an/aus + Dauer. |
| Layout-Modus (`layout=volume|media|action|status` mit Header, Icon, Balken) | Das sollte **der einzige** Display-Mechanismus für Feedback werden. Die semantischen Felder sind ein gutes Protokoll-Design. |
| Text-Zeilen mit Größe 1/2, Ausrichtung, horizontales Scrollen langer Zeilen | Für Standard-Screen behalten. Ein Format (einfacher Text, z. B. `# ` für groß, `>` für zentriert), kein HTML/Markdown/Baukasten-Trio. |
| Live-OLED-Vorschau in der App | Behalten, aber aus derselben Render-Logik wie die Firmware (oder Firmware rendert, App zeigt Pixel-Abbild). |
| Kontextabhängige Screens/Profile (Bedingungen: Prozess läuft, im Vordergrund, Vollbild, Medien aktiv) | Die Bedingungs-Engine ist sauber (rein, testbar). Im Neubau aber besser für **automatischen Profilwechsel nach Vordergrund-App** nutzen als für Idle-Screen-Rotation. Eine Bedingung „Prozess im Vordergrund“ reicht anfangs. |
| Vordergrundfenster-/Vollbild-Erkennung | Basis für App-abhängige Profile. `WindowsForegroundWindowProbe.cs` |
| Now Playing | Idee behalten, Umsetzung ersetzen: Windows Media Session (GSMTC) statt Spotify-Fenstertitel – app-unabhängig, inkl. Play-Status. |
| Systemmetriken (CPU, RAM, Netzwerk, Akku) | Nur falls ein Status-Screen gewünscht ist; sonst weglassen. `SystemMetricsSampler.cs` |
| Hardware-Layout vs. Benutzerprofil getrennt (Projektplan) | Konsequent umsetzen: Gerät meldet Hardware, Profil beschreibt nur Bedeutung. |

## 4. Streichen bzw. bewusst nicht wieder bauen

- HTML-Subset-Renderer, Markdown-Pseudocode und Baukasten für Display-Inhalte.
- Frei konfigurierbare Display-Trigger mit Event-Typen, Bedingungen
  (`inRange`, `increasing`, `decreasing`), Schwellen-Akkumulation, Sequenzen,
  Reihenfolge, Presets, Match-Diagnose.
- Idle-Screen-Rotation, Prioritäten, Exklusiv-Modus, Aktivierungsverzögerung,
  Mindest-Standzeit.
- Parallele Display-Pfade (`template`, `message`, `profile`, `media`,
  `action-result`-Fallback, `preferredDisplay`, `preferred=false`).
- Config-Migratoren für Altformate (Neubau startet mit neuem, versioniertem
  Schema; ggf. einmaliger Import der Bindings).
- Doppelte Ausgabe auf `Serial` und `Serial0` in der Firmware (war
  Debug-Hilfe für den ersten Aufbau).
- Root-ViewModel als Orchestrator (`ShellViewModel.Runtime.cs`).

## 5. Lehren für die Architektur des Neubaus

1. **Ein klarer Laufzeitkern ohne UI**: `DeviceService` (Verbindung, Events) →
   `InputRouter` (Profil, Binding) → `ActionRunner` → `FeedbackService` (Display).
   Die UI liest und ändert nur Config und beobachtet den Kern. Das alte Projekt
   hat diese Teile über Jahre aus dem ViewModel herausgelöst – im Neubau von
   Anfang an so schneiden.
2. **Display-Verantwortung eindeutig festlegen.** Entweder die Firmware rendert
   wenige feste Screen-Typen aus semantischen Feldern (empfohlen), oder die App
   rendert alles. Nicht beides.
3. **Hardware-Fähigkeiten vom Gerät beziehen** (`hello` mit Tastenzahl,
   Slider-Zahl, ggf. Layout) statt 20/3 hart zu codieren. Zielhardware jetzt:
   16 Tasten (5×4 minus 2×2 OLED-Block), 3 Slider, SSD1306 128×64.
4. **Protokoll v2 von Beginn an klein halten**: `hello`, `hello-ack`, `ping`/
   `pong`, `button`, `slider`, `error`, `screen` (ein Display-Befehl mit
   `type=`), `config`. Format `type;key=value` beibehalten.
5. **Fehlende Kernfeatures priorisieren statt Display-Features**: Multi-Step-
   Makros (Sequenz mit Delays), Text eingeben, Programm/Skript mit Argumenten,
   HTTP-Request, Long-Press/Doppeldruck, Tasten-Ebenen (Fn-Layer), automatischer
   Profilwechsel nach Vordergrund-App, Lautstärke für Vordergrund-App bzw. „alle
   außer Vordergrund-App“.
6. **Firmware von Anfang an modular** (`input`, `slider_filter`, `protocol`,
   `display`, `hid_fallback`) und Konstanten zentral, weil Pinout und Tastenzahl
   sich mit dem Gehäuse geändert haben.
7. **Vertikale Meilensteine**: erst Taste → Action → OLED-Feedback auf echter
   Hardware fertig, dann erst Komfort.

## 6. Entscheidungen (2026-10-04)

- **Tasten:** final 16 Tasten + 3 Slider + SSD1306 128×64.
- **Display:** feste Firmware-Screens, die App sendet nur semantische Felder.
- **Plattform:** nur Windows. Linux ist vorerst irrelevant.
- **Offline-Fallback:** soll so viel wie realistisch abdecken (Ziel: Bindings
  im Flash, echte Shortcuts per HID-Tastatur), hat aber **keine Priorität**.
  Das Protokoll und das Binding-Modell sollen das später erlauben, ohne es jetzt
  zu bauen.
- **Stack App:** bleibt C#/.NET 10 + Avalonia (entschieden, nicht ändern).
- **Stack Firmware:** Wechsel von Arduino IDE auf PlatformIO (weiterhin
  Arduino-Framework), modular mit nativen Unit-Tests.
- Umsetzungsplan: [REWRITE_PLAN.md](REWRITE_PLAN.md).

### Stack-Begründung (Kurzfassung)

Die schwierigen Teile der App sind Windows-APIs: CoreAudio-Sessions,
Media-Session (GSMTC), `SendInput`, Vordergrundfenster, Serial. In .NET sind
alle direkt nutzbar (NAudio, WinRT-Projektion über
`net10.0-windows10.0.19041.0`, P/Invoke), und der alte Code ist portierbar.
Tauri/Rust oder Electron bringen hier nur Mehraufwand. Innerhalb von .NET ist
die UI-Wahl nachrangig: Avalonia funktioniert und ist bekannt. WPF + WPF-UI
wäre die Alternative für ein natives Windows-11-Aussehen. WinUI 3 wird wegen
Packaging-/Tray-Reibung nicht empfohlen.
