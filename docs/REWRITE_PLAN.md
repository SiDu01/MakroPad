# MakroPad – Neubau-Plan

Stand: 2026-10-29 (v0.4.0). Grundlage: [REWRITE_NOTES.md](REWRITE_NOTES.md).

## Leitlinie

> Taste oder Slider bedienen → am PC passiert zuverlässig etwas → das OLED
> bestätigt es kurz.

Alles, was diesen Kern nicht verbessert, kommt erst nach Meilenstein 3.
Jeder Meilenstein endet mit einem Test auf echter Hardware.

## Rahmen

| Thema | Entscheidung |
| --- | --- |
| Hardware | 16 MX-Tasten (5×4-Raster, oben rechts 2×2 durch OLED ersetzt), 3 Slider (B103), SSD1306 128×64 I2C, ESP32-S3-DevKitC-1 |
| Firmware | PlatformIO, Arduino-Framework, modular, native Unit-Tests |
| App | C#/.NET 10 + Avalonia, nur Windows (TFM `net10.0-windows10.0.19041.0`) |
| Display | Feste Screens in der Firmware, die App sendet nur semantische Felder |
| Offline | Medien-Fallback per HID wie bisher; erweiterter Offline-Betrieb (Bindings im Flash) ist vorbereitet, aber nicht geplant |
| Linux | nicht vorgesehen |

## Tastenlayout

Physisches Raster (Ansicht von oben, `·` = OLED):

```text
 1  2  3  ·  ·
 4  5  6  ·  ·
 7  8  9 10 11
12 13 14 15 16
```

Die IDs zählen zeilenweise von links oben. Die App beschreibt dieses Layout als
Gerätedefinition (Raster 5×4, OLED-Block bei Spalte 4–5 / Zeile 1–2), das Gerät
meldet `buttons=16;sliders=3`.

**Pinout:** Von den bisher 20 Tasten-GPIOs werden 4 frei. Vorschlag: GPIO47/48
(48 = RGB-LED bei DevKitC v1.0) und GPIO41/42 (JTAG) nicht mehr nutzen.
**Vor M1 gegen die tatsächliche Verdrahtung abgleichen** und in
`docs/HARDWARE.md` festhalten.

## Repository-Struktur

```text
MakroPad/
  docs/                    PROTOCOL.md (v2), HARDWARE.md, CONFIG.md, REWRITE_*.md
  hardware/case/           OpenSCAD-Gehäuse
  installer/               Inno-Setup-Installer, build-release.ps1
  firmware/                PlatformIO-Projekt
    platformio.ini
    include/config.h       Pins, Zeiten, Filterkonstanten an einer Stelle
    src/main.cpp           nur setup()/loop(), verdrahtet die Module
    src/input, hostlink, display, hid   Geräte-Module (Arduino)
    lib/makropad_core/     reine Logik: Entprellung, Slider-Filter, Protokoll, Uhr, Display-Text
    test/                  native Tests für lib/makropad_core
  app/
    MakroPad.sln
    MakroPad.Core/         Modelle, Protokoll, Config, Runtime (ohne OS-Aufrufe)
    MakroPad.Device/       Serial-Verbindung, Port-Suche, Simulator
    MakroPad.Windows/      CoreAudio, SendInput, Media-Session, Vordergrundfenster, Shell
    MakroPad.App/          Avalonia-UI, Tray, Composition Root
    MakroPad.Core.Tests/
    MakroPad.Device.Tests/
    MakroPad.App.Tests/
```

Laufzeitkern in `MakroPad.Core/Runtime`, von der UI getrennt:

```text
DeviceService ──InputEvent──▶ InputRouter ──Binding──▶ ActionRunner
     ▲                         (Profil)                    │ ActionResult
     │                                                     ▼
     └──────────── screen/state ◀──────────────── FeedbackService
```

Die UI liest/schreibt nur die Config über einen `ConfigService` und beobachtet
den Kern (Events für Verbindung, Inputs, Ergebnisse). Kein ViewModel
orchestriert Geräte-Logik.

## Protokoll v2 (Entwurf; gültig ist [PROTOCOL.md](PROTOCOL.md))

Format bleibt `type;key=value`, Percent-Encoding wie v1, Zeilenende `\n`.

Gerät → App:

| Nachricht | Felder |
| --- | --- |
| `hello` | `proto=2`, `fw`, `device=makropad`, `buttons=16`, `sliders=3`, `display=ssd1306-128x64` |
| `button` | `id`, `state=down\|up` |
| `slider` | `id`, `value` (0–1000, gefiltert) |
| `pong` | `id` |
| `error` | `code`, `message` |
| `log` | `message` (nur Debug) |

App → Gerät:

| Nachricht | Felder | Zweck |
| --- | --- | --- |
| `hello-ack` | `proto=2`, `app` | Handshake |
| `ping` | `id` | Heartbeat |
| `state` | `profile`, `time`, `date`, `artist`, `title` (alle optional) | Dauerhafter Zustand für den Idle-Screen; nur geänderte Felder senden |
| `screen` | `type=volume\|action\|media\|profile\|status`, `title`, `text`, `value`, `icon=ok\|fail\|warn\|sync`, `ms` | Temporäres Overlay, danach zurück zum Idle-Screen |
| `clear` | – | Overlay beenden |
| `display` | `on=1\|0` | Display ein/aus (seit v0.4.0) |

Regeln: Unbekannte Typen/Keys ignorieren. Platzhalter gibt es nicht mehr – die
Firmware zeichnet den Idle-Screen aus `state` (Profil, laufende Uhr, Track) mit
festem Layout. Firmware-Uhr läuft ohne App weiter. Eine Lautstärkeanzeige im
Idle-Screen ist bewusst entfallen.

Der Slider-Wert wird in der Firmware auf 0–1000 normiert, damit die App keine
ADC-Details kennen muss.

## Meilensteine

### M0 – Grundgerüst

- Alten Stand taggen (`legacy-v0.2.0`), auf Branch `rewrite` arbeiten.
- `app/` und `firmware/` nach `legacy/` verschieben (zum Nachschlagen beim
  Portieren).
- PlatformIO installieren (VS-Code-Extension oder `pip install platformio`),
  leeres Firmware-Projekt mit `config.h`, baut für `esp32-s3-devkitc-1`.
- Neue Solution mit den vier Projekten + Tests, baut und testet leer.
- `docs/PROTOCOL.md` auf v2 umschreiben (aus dem Entwurf oben).
- Pinout für 16 Tasten festlegen.

**Fertig, wenn:** `pio run`, `pio test -e native`, `dotnet build`, `dotnet test`
laufen grün.

**Status (2026-10-04): erledigt.** Firmware baut, 6 native Tests
(Slider-Filter) grün; Solution baut, 6 Tests (`ProtocolCodec`) grün; App
startet mit leerem Fenster. `MakroPad.Device.Tests` wird in M1 mit dem
Simulator angelegt.

### M1 – Vertikaler Durchstich

Firmware:
- Buttons (Entprellung) und Slider-Filterkette aus `legacy` portieren, die
  Filterlogik als reine Funktion mit nativen Tests.
- Protokoll v2: `hello`, `ping`/`pong`, Host-Timeout, `button`, `slider`.
- Display: Idle-Screen (Profil + Uhr) und Overlay `screen;type=volume|action`.
- HID-Fallback (Tasten 1–7 Medien, Slider 1 relative Lautstärke) portieren.

App:
- `ProtocolCodec`, Port-Suche, Serial-Verbindung, Heartbeat, Simulator
  portieren (aus `legacy`).
- Runtime-Kern mit fest verdrahteter Test-Belegung: Taste 1 →
  `media.playPause`, Slider 1 → `system.volume`.
- Minimalfenster: Verbindungsstatus + Event-Log.

**Fertig, wenn:** Auf der echten Hardware Taste 1 Play/Pause auslöst, Slider 1
die Systemlautstärke ruckelfrei setzt und das OLED beides kurz anzeigt; Abziehen
und Wiederanstecken verbindet automatisch neu.

**Status (2026-10-04): erledigt, auf Hardware bestätigt** (Taste 1, Slider 1
inkl. 0/100 %, Reconnect nach Abziehen, HID-Fallback ohne App). Firmware-Module
(`hostlink`, `input`, `display`, `hid`) und App-Kern (`DeviceSession`,
`InputRouter`, `LatestWinsQueue`, `MakroPadRuntime`, Windows-Actions für
Medientasten und Systemlautstärke) sind fertig. 20 native Firmware-Tests und
30 App-Tests grün, inkl. Ende-zu-Ende mit dem Simulator
(`dotnet run --project app\MakroPad.App -- --simulator`).

### M2 – Profile, Bindings, Actions

- Config v1 (versioniert, `schemaVersion`), atomares Speichern, Default-Config.
- Profile mit Bindings `input → action + parameter`, aktives Profil.
- Action-Katalog mit Metadaten (Name, Parameter, Pflichtfelder, Beispiel) als
  einzige Quelle für Ausführung, Editor und Validierung.
- Actions: `media.*`, `system.volume`, `app.volume`, `keyboard.shortcut`,
  `system.openUrl`, `system.openApp`, `profile.next` / `profile.set`.
- Feedback: Ergebnis jeder Action → `screen`-Overlay (ein Ein/Aus-Schalter pro
  Ereignistyp, sonst nichts konfigurierbar).
- Tray-Betrieb, Start im Tray, Autostart.

**Fertig, wenn:** Alle Actions per Hand-Config auf der Hardware funktionieren,
Profilwechsel per Taste geht und auf dem OLED erscheint.

**Status (2026-10-04): erledigt, auf Hardware bestätigt** (alle neuen Actions,
Live-Reload, Profilwechsel per Taste). `config.json`
(Schema 1, Live-Reload, Validierung gegen `ActionCatalog`, Referenz in
[CONFIG.md](CONFIG.md)), getrennte `state.json`, `ProfileManager`, neue
Actions `app.volume`, `keyboard.shortcut`, `system.openUrl`,
`system.openApp`, `profile.next`, `profile.set`, OLED-Feedback abschaltbar,
Tray mit Profilmenü, Autostart. 64 App-Tests grün.

### M3 – Bedienoberfläche

- Pad-Ansicht mit dem 16-Tasten-Layout inkl. OLED-Lücke und 3 Slidern.
- Klick auf Taste/Slider → Inspector mit Action-Editor aus den Metadaten,
  Shortcut-Recorder, App-/Datei-Picker, Audio-Session-Auswahl für `app.volume`.
- Live-Highlight beim Drücken, Test-Modus (Action ausführen ohne Hardware).
- Profilleiste (anlegen, umbenennen, löschen, wechseln).
- Diagnose-Seite: Rohprotokoll, Verbindung, aktive Audio-Sessions.

**Fertig, wenn:** Eine komplette Belegung ohne Handbearbeitung der JSON möglich
ist.

**Status (2026-10-04): erledigt, auf Hardware bestätigt** (inkl. Aufnahme von
Win-Kombinationen über einen Low-Level-Hook auf eigenem Thread). Pad-Seite mit dem
Gerät in Gehäusefarben, pixelgenauem OLED-Spiegel (`OledMirror`), Inspector
(Aktion, Parameter, Shortcut-Aufnahme, Audio-Sessions, Dateiauswahl, Jetzt
auslösen), Profilleiste (anlegen, umbenennen, duplizieren, verschieben,
löschen), Diagnose (Verbindung, Config-Probleme, Audio, Protokoll, Simulator)
und Einstellungen. Gestaltung: [UI_DESIGN.md](UI_DESIGN.md).

### M4 – Kontext

- Now Playing über Windows Media Session (GSMTC) → `state;artist;title`,
  Track-Wechsel optional als `screen;type=media`.
- Automatischer Profilwechsel nach Vordergrund-App (Regel: Prozess →
  Profil, Fallback-Profil).
- Lautstärke-Ziele: Vordergrund-App, „alle außer Vordergrund-App“.

**Status (2026-10-16): erledigt, auf Hardware bestätigt.** `MediaSessionWatcher`
(GSMTC) → `state;artist;title` plus Einblendung bei neuem Titel,
`ForegroundWatcher` + `AutoProfileSwitcher` mit Regeln und Ausweichprofil
(Einstellungen), Actions `app.volume.foreground` und `app.volume.others`.

### M5 – Makros und Eingabe-Gesten

- Action-Sequenzen (mehrere Schritte, Verzögerung dazwischen).
- Text eingeben, Befehl/Skript mit Argumenten, HTTP-Request.
- Long-Press und Doppeldruck als eigene Inputs (`button:3:long`).

**Status (2026-10-29): erledigt, auf Hardware bestätigt.** `GestureDetector`
(`button:N`, `button:N:long`, `button:N:double`, Zeiten in den
Einstellungen), Makros mit Pausen (`ActionRunner`, Editor im Inspector),
`keyboard.text`, `system.run`, `http.request`. Inspector mit Gesten-Reitern.

### M6 – Erweiterter Offline-Betrieb (optional)

- Bindings, die als HID abbildbar sind (Medien, Tastenkombinationen), ins
  Gerät übertragen und im NVS speichern; HID-Tastatur zusätzlich zu
  Consumer-Control.

**Status (2026-10-29): zurückgestellt.** Der bestehende HID-Fallback (Tasten
1–7 Medien, Slider 1 Lautstärke) deckt den Betrieb ohne App ab.

### Nach M5 (v0.4.0)

- Aktion `toggle` („Abwechselnd“): jeder Druck führt den nächsten Schritt aus.
- Aktion `binding.assign` („Taste belegen“): die nächste gedrückte Taste im
  Schnell-Fenster belegen, gespeichert erst mit „Speichern“.
- Tasten, Slider und Display einzeln abschaltbar (`settings.disabledInputs`,
  `settings.displayEnabled`, Protokoll `display;on`).

Alles auf Hardware bestätigt.

## Was aus dem alten Stand portiert wurde

Quellen im Tag `legacy-v0.2.0` (der Ordner `legacy/` wurde nach M4 gelöscht).

| Ziel | Quelle |
| --- | --- |
| Slider-Filterkette, Entprellung, Uhr, Scrolling, Header/Icon/Balken-Zeichnung, HID-Fallback | `firmware/MakroPadFirmware/MakroPadFirmware.ino` |
| `ProtocolCodec` (+ Tests) | `app/MakroPad.Core/Protocol` |
| Port-Suche, Serial-Verbindung, Simulator | `app/MakroPad.Device` |
| Audio/Lautstärke, Tastatur, Medien, Shell-Start | `app/MakroPad.Actions/Windows*.cs` |
| Vordergrundfenster/Vollbild | `app/MakroPad.App/Services/WindowsForegroundWindowProbe.cs` |
| Atomares Config-Speichern | `app/MakroPad.Core/Configuration/MakroPadConfigStore.cs` |
| Theme (Grau-Teal, Dark/Light), Icons | `app/MakroPad.App/Themes` |

Nicht portiert: Display-Trigger, Idle-Screen-Engine, HTML/Markdown-Renderer,
Baukasten, Config-Migratoren (siehe REWRITE_NOTES Abschnitt 4).
