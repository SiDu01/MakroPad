# Konfiguration

Die App liest `%APPDATA%\MakroPad\config.json`. Änderungen werden beim Speichern
sofort übernommen, ein Neustart ist nicht nötig. Fehler zeigt das App-Fenster
unter „Probleme in config.json“ an; ist die Datei kein gültiges JSON, bleibt
sie unverändert und es gelten bis zur Korrektur die Standardwerte.

Öffnen: App-Fenster → „Konfiguration bearbeiten“ oder Tray-Menü.

Daneben verwaltet die App `state.json` (aktives Profil, letzter COM-Port). Die
Datei muss nicht bearbeitet werden.

Eine Konfiguration der App 0.2 (ohne `schemaVersion`) wird beim ersten Start
als `config.legacy-v0.2.json` gesichert und durch die Standardkonfiguration
ersetzt.

## Aufbau

```jsonc
{
  "schemaVersion": 1,
  "settings": {
    "startInTray": false,          // ohne Fenster im Infobereich starten
    "startWithWindows": false,     // Autostart für den aktuellen Benutzer
    "sliderDeadbandPercent": 2,    // Mindeständerung für Lautstärke-Slider (0–25)
    "longPressMs": 500,            // ab so langem Halten zählt ein langer Druck (200–3000)
    "doublePressMs": 300,          // zweiter Druck innerhalb dieser Zeit = Doppeldruck (100–1000)
    "theme": "system",             // system, light oder dark
    "debugMode": false,            // Testhilfen (Startoptionen, zusätzliche Diagnose)
    "feedback": {                  // kurze OLED-Einblendungen
      "actions": true,
      "volume": true,
      "errors": true,
      "profileChange": true,
      "trackChange": true          // Titel und Künstler bei einem neuen Titel
    }
  },
  "autoProfile": {                 // Profil nach Programm im Vordergrund
    "enabled": true,
    "rules": [ { "process": "Discord", "profile": "Discord" } ],
    "fallbackProfile": null        // Profil für alle anderen Programme, null = beibehalten
  },
  "profiles": [
    {
      "name": "Default",
      "bindings": [
        { "input": "button:1", "action": "media.playPause" },
        { "input": "button:5", "action": "keyboard.shortcut",
          "parameters": { "keys": "Ctrl+Shift+Esc", "label": "Task-Manager" } },
        { "input": "button:16", "action": "profile.next" },
        { "input": "slider:1", "action": "system.volume" },
        { "input": "slider:2", "action": "app.volume",
          "parameters": { "process": "Spotify", "label": "Musik" } }
      ]
    }
  ]
}
```

Kommentare (`//`) und abschließende Kommas sind erlaubt.

## Eingaben

Tasten zeilenweise von links oben (`·` = OLED), Slider von links nach rechts:

```text
 1  2  3  ·  ·
 4  5  6  ·  ·
 7  8  9 10 11
12 13 14 15 16
```

`button:1` … `button:16`, `slider:1` … `slider:3`. Jede Eingabe darf pro Profil
nur einmal belegt sein.

Jede Taste hat drei Gesten, die getrennt belegt werden:

| Eingabe | Geste |
| --- | --- |
| `button:5` | Drücken |
| `button:5:long` | Lang drücken (mindestens `longPressMs`) |
| `button:5:double` | Doppelt drücken (zweiter Druck innerhalb `doublePressMs`) |

Ohne lange oder doppelte Belegung löst eine Taste sofort beim Drücken aus. Ist
eine davon belegt, muss die App erst unterscheiden: „Drücken“ kommt dann beim
Loslassen bzw. nach Ablauf von `doublePressMs`.

## Aktionen

`label` (in der App „Beschriftung“) ist bei allen Aktionen mit Parametern
optional. Der Text steht dann auf der Taste in der App und in der
OLED-Einblendung.

| Aktion | Eingabe | Parameter | Wirkung |
| --- | --- | --- | --- |
| `media.playPause` | Taste | – | Medientaste Play/Pause |
| `media.nextTrack` | Taste | – | Nächster Titel |
| `media.previousTrack` | Taste | – | Vorheriger Titel |
| `media.stop` | Taste | – | Stopp |
| `media.mute` | Taste | – | Systemton stumm |
| `media.volumeUp` | Taste | – | Lauter (ein Schritt) |
| `media.volumeDown` | Taste | – | Leiser (ein Schritt) |
| `system.volume` | Slider | – | Lautstärke des Standard-Ausgabegeräts |
| `app.volume` | Slider | `process` (z. B. `Spotify`, ohne `.exe`) | Lautstärke aller Audio-Sessions des Programms; es muss gerade Audio ausgeben |
| `app.volume.foreground` | Slider | – | Lautstärke des Programms, das gerade im Vordergrund ist |
| `app.volume.others` | Slider | – | Lautstärke aller Programme außer dem im Vordergrund |
| `keyboard.shortcut` | Taste | `keys` (z. B. `Ctrl+Shift+Esc`) | Tastenkombination an das aktive Fenster |
| `system.openUrl` | Taste | `url` (http/https) | Adresse im Standardbrowser öffnen |
| `system.openApp` | Taste | `path`, optional `args` | Programm starten oder Datei öffnen; Umgebungsvariablen wie `%USERPROFILE%` werden aufgelöst |
| `keyboard.text` | Taste | `text` | Tippt den Text in das aktive Fenster, unabhängig vom Tastaturlayout; `\n` wird zu Enter |
| `system.run` | Taste | `command` | Führt eine Befehlszeile unsichtbar über `cmd.exe` aus. Endet sie innerhalb von 5 s mit einem Fehlercode, meldet das OLED den Fehler |
| `http.request` | Taste | `url`, optional `method`, `body`, `contentType` | Sendet eine Webanfrage (z. B. Home Assistant, Webhooks). Ohne `method` GET bzw. mit `body` POST, Inhalt standardmäßig `application/json`, 10 s Zeitlimit |
| `macro` | Taste | `steps` (siehe unten) | Führt mehrere Aktionen nacheinander aus |
| `profile.next` | Taste | – | Nächstes Profil (in Listenreihenfolge, danach wieder das erste) |
| `profile.set` | Taste | `name` | Bestimmtes Profil aktivieren |

## Makros

Ein Makro führt bis zu 50 Schritte nacheinander aus. Jeder Schritt ist eine
Tasten-Aktion mit ihren Parametern oder eine Pause (`delay` mit `ms`, höchstens
60000). Schlägt ein Schritt fehl, bricht das Makro ab und das OLED nennt den
Schritt. Makros in Makros und Slider-Aktionen sind nicht möglich.

```jsonc
{ "input": "button:9:long", "action": "macro", "parameters": { "label": "Stream" },
  "steps": [
    { "action": "system.openApp", "parameters": { "path": "obs64.exe" } },
    { "action": "delay", "parameters": { "ms": "3000" } },
    { "action": "keyboard.shortcut", "parameters": { "keys": "Ctrl+F9" } },
    { "action": "keyboard.text", "parameters": { "text": "Bin live!\n" } }
  ] }
```

## Automatischer Profilwechsel

Kommt ein Programm in den Vordergrund, für das eine Regel existiert, wird dessen
Profil aktiv. Für Programme ohne Regel gilt `fallbackProfile`; ist es `null`,
bleibt das aktuelle Profil. Gewechselt wird nur, wenn ein anderes Programm in
den Vordergrund kommt: Ein von Hand gewähltes Profil bleibt also aktiv, bis du
das Programm wechselst. Das MakroPad-Fenster selbst löst keinen Wechsel aus.
`process` ist der Prozessname ohne `.exe` (siehe App → Diagnose → Vordergrund).

## Now Playing

Die App liest Titel und Künstler aus der Medienwiedergabe von Windows (dieselbe
Anzeige wie im Lautstärke-Popup) – das funktioniert mit Spotify, Browsern, VLC
und anderen Playern. Der Ruhebildschirm zeigt den Titel, ein neuer Titel wird
kurz eingeblendet (`feedback.trackChange`).

### Tasten für `keyboard.shortcut`

Modifikatoren: `Ctrl` (auch `Strg`), `Shift`, `Alt`, `Win`.
Tasten: `A`–`Z`, `0`–`9`, `F1`–`F24`, `Enter`, `Esc`, `Tab`, `Space`,
`Backspace`, `Delete`, `Insert`, `Home`, `End`, `PageUp`, `PageDown`, `Up`,
`Down`, `Left`, `Right`, `PrintScreen`, `Pause`, `CapsLock`, `NumLock`,
`ScrollLock`, `Apps`, `Plus`, `Minus`, `Comma`, `Period`.
Groß-/Kleinschreibung ist egal, Teile werden mit `+` verbunden.
