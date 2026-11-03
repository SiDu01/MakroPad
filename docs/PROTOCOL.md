# Serial-Protokoll v2

Kommunikation zwischen Firmware (ESP32-S3) und Companion-App über USB CDC
oder Bluetooth LE.
Version 1 (bis App/Firmware 0.2.0) ist über das Git-Tag `legacy-v0.2.0`
nachlesbar.

## Grundsätze

- Das Gerät meldet **Eingaben**, die App entscheidet, was passiert.
- Die App sendet dem Display **semantische Felder**, keine Pixel und keine
  Platzhalter. Die Firmware zeichnet daraus feste Screens.
- Klein bleiben: Neue Nachrichten nur, wenn ein Meilenstein sie braucht.

## Transport und Format

- USB CDC, 115200 Baud (bei CDC praktisch ohne Bedeutung).
- Oder Bluetooth LE, Nordic UART Service (`6e400001-b5a3-f393-e0a9-e50e24dcca9e`):
  Die App schreibt auf RX (`…0002…`), das Gerät sendet per Notification auf TX
  (`…0003…`). Zeilen werden auf MTU − 3 Bytes zerteilt und beim Empfänger am
  `
` wieder zusammengesetzt. Zugriff nur über eine verschlüsselte, gebondete
  Verbindung (LE Secure Connections mit Passkey; das Gerät zeigt den Code auf
  dem OLED).
- Vor dem Handshake sendet das Gerät auf beiden Kanälen. Der Kanal, über den
  `hello-ack` kommt, ist danach der einzige; Nachrichten auf dem anderen werden
  ignoriert. Die App versucht zuerst USB, dann Bluetooth.
- USB hat Vorrang: Ist die App über Bluetooth verbunden, sendet das Gerät auf
  USB weiter jede Sekunde `hello`. Ein `hello-ack` über USB übernimmt die
  Verbindung, ein `hello-ack` über Bluetooth übernimmt eine USB-Verbindung nie.
  Die App auf Bluetooth meldet dann `Unresponsive` und verbindet sich wieder,
  sobald USB getrennt ist.
- Eine Nachricht pro Zeile, Ende `\n` (`\r\n` wird toleriert).
- Führende/abschließende Leerzeichen werden entfernt, leere Zeilen ignoriert.
- Maximale Zeilenlänge: 256 Zeichen.

```text
type;key=value;key=value
```

- Erstes Segment: Nachrichtentyp. Danach `key=value`-Paare, getrennt durch `;`.
- Keys: ASCII-Buchstaben, Ziffern, `_`, `-`. Values: UTF-8.
- In Values werden percent-kodiert: `%` → `%25`, `;` → `%3B`, `=` → `%3D`,
  `\r` → `%0D`, `\n` → `%0A`.
- Unbekannte Typen und Keys werden ignoriert. Bei doppelten Keys gilt der
  letzte.

## Verbindung

1. Das Gerät sendet jede Sekunde `hello`, solange keine App bestätigt hat.
2. Die App antwortet mit `hello-ack` (auf **jedes** `hello`, auch wenn sie sich
   für verbunden hält – das Gerät könnte neu gestartet sein). Passt `proto` nicht, sendet sie nichts
   weiter und zeigt den Fehler an.
3. Die App sendet direkt danach einen vollständigen `state`.

Heartbeat:

- Jede gültige Nachricht der Gegenseite gilt als Lebenszeichen. Vor dem
  Handshake ignoriert die Firmware alles außer `hello-ack`.
- Die App sendet `ping`, wenn sie 2 s nichts gehört hat, und meldet die
  Verbindung nach 6 s Stille als `Unresponsive`.
- Die Firmware fällt nach 6 s ohne App-Nachricht in den Zustand „keine App“
  zurück (HID-Fallback aktiv, `hello` wird wieder gesendet).

## Gerät → App

| Typ | Felder | Bedeutung |
| --- | --- | --- |
| `hello` | `proto=2`, `fw`, `device=makropad`, `buttons`, `sliders`, `display` | Gerät meldet sich |
| `button` | `id` (1–16), `state=down\|up` | Entprellter Zustandswechsel |
| `slider` | `id` (1–3), `value` (0–1000) | Gefilterter, normierter Wert; nur bei relevanter Änderung |
| `pong` | `id` | Antwort auf `ping` |
| `error` | `code`, `message` | Firmware-/Hardwarefehler, z. B. `display_init_failed` |
| `log` | `message` | Debug-Ausgabe, die App zeigt sie nur in der Diagnose |

Beispiel:

```text
hello;proto=2;fw=0.5.0;device=makropad;buttons=16;sliders=3;display=ssd1306-128x64
button;id=1;state=down
button;id=1;state=up
slider;id=1;value=734
```

## App → Gerät

| Typ | Felder | Bedeutung |
| --- | --- | --- |
| `hello-ack` | `proto=2`, `app` | Handshake bestätigen |
| `ping` | `id` | Heartbeat |
| `state` | `profile`, `time` (`HH:mm:ss`), `date` (`dd.MM.yyyy`), `artist`, `title` | Dauerhafter Zustand für den Idle-Screen. Alle Felder optional; fehlende Felder bleiben unverändert, ein leerer Wert löscht. |
| `screen` | `type`, `title`, `text`, `value`, `icon`, `ms` | Temporäres Overlay, danach zurück zum Idle-Screen |
| `clear` | – | Overlay sofort beenden |
| `display` | `on=1\|0` | Display ein- oder ausschalten. Trennt sich die App, schaltet die Firmware es wieder ein; die App sendet `on=0` deshalb nach jedem Handshake erneut. |

### `screen`-Typen

| `type` | Verwendete Felder | Darstellung |
| --- | --- | --- |
| `volume` | `title`, `text`, `value` (0–100) | Header, Beschriftung, große Prozentzahl, Balken |
| `action` | `title`, `text`, `icon` | Header, Icon, Meldung |
| `media` | `title` (Track), `text` (Künstler) | Fester Header „Wiedergabe“, darunter Track und Künstler |
| `profile` | `title`, `text` | Header, Profilname groß |
| `status` | `title`, `text`, `icon` | Header, Icon, Statusmeldung |

- `icon`: `ok`, `fail`, `warn`, `sync`. Fehlt es, wird kein Icon gezeichnet.
- `ms`: Anzeigedauer, Standard 1500; `0` = bis zum nächsten `screen` oder `clear`.
  Ein neues `screen` ersetzt das laufende.
- Zu lange Texte scrollen horizontal.
- Unbekannte Typen werden als `status` gezeichnet.

Beispiel:

```text
hello-ack;proto=2;app=0.5.0
state;profile=Default;time=18:42:05;date=04.10.2026
screen;type=volume;title=Lautstärke;text=System;value=72;ms=1200
screen;type=action;title=Aktion;text=Play%2FPause;icon=ok
```

## Idle-Screen

Die Firmware zeichnet ihn fest aus dem zuletzt empfangenen `state`:

- Kopfzeile: Profilname
- Groß: Uhrzeit (läuft ohne App aus der letzten Host-Zeit weiter)
- Darunter: Datum, oder `artist – title`, wenn ein Track bekannt ist
- Ohne App: Hinweis „Keine App“ statt Profilname

## HID-Fallback (ohne App)

Solange keine App bestätigt hat, sendet die Firmware zusätzlich USB-HID-
Consumer-Control-Events:

| Eingabe | Aktion |
| --- | --- |
| Taste 1 | Play/Pause |
| Taste 2 | Nächster Titel |
| Taste 3 | Vorheriger Titel |
| Taste 4 | Stopp |
| Taste 5 | Stumm |
| Taste 6 | Leiser |
| Taste 7 | Lauter |
| Slider 1 | Relative Systemlautstärke (Schritte, keine absolute Position) |

Die serielle `button`/`slider`-Meldung wird trotzdem gesendet.
