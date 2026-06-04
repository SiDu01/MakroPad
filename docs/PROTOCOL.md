# Serial Protocol

## Ziel

Dieses Dokument beschreibt das erste Kommunikationsprotokoll zwischen
ESP32-S3-Firmware und Desktop-Companion-App.

Das Protokoll soll:

- mit Arduino einfach zu erzeugen und zu parsen sein.
- in C#/.NET robust modellierbar sein.
- im Serial Monitor lesbar bleiben.
- versionierbar sein.
- spaeter ohne kompletten Neustart des Projekts erweitert werden koennen.

## Transport

Der erste Transport ist USB CDC/Serial.

Rahmenbedingungen:

- Baudrate fuer USB CDC ist praktisch nicht kritisch, wir verwenden trotzdem
  `115200` als Default.
- Jede Nachricht endet mit `\n`.
- Sender duerfen optional `\r\n` verwenden.
- Empfaenger entfernen fuehrende und abschliessende Leerzeichen.
- Leere Zeilen werden ignoriert.
- Maximal empfohlene Zeilenlaenge fuer Version 1: 256 Zeichen.

## Format

Version 1 nutzt ein zeilenbasiertes Key-Value-Format:

```text
type;key=value;key=value
```

Beispiele:

```text
hello;proto=1;fw=0.1.0;device=makropad-s3
button;id=1;state=down
slider;id=1;value=1834
display;mode=media;artist=Daft Punk;title=Digital Love
```

Regeln:

- Das erste Segment ist der Nachrichtentyp.
- Weitere Segmente sind `key=value` Paare.
- Segmente werden durch Semikolon getrennt.
- Keys bestehen aus ASCII-Buchstaben, Zahlen, `_` oder `-`.
- Values sind UTF-8 Text.
- `;`, `=`, `\r`, `\n` und `%` muessen in Values percent-encodiert werden.

Percent-Encoding:

| Zeichen | Kodierung |
| --- | --- |
| `%` | `%25` |
| `;` | `%3B` |
| `=` | `%3D` |
| `\r` | `%0D` |
| `\n` | `%0A` |

Beispiel:

```text
display;mode=message;text=Volume%3D50%25
```

entspricht:

```text
Volume=50%
```

## Verbindungsaufbau

Nach dem Start sendet der ESP regelmaessig eine `hello` Nachricht, bis die App
mit `hello-ack` antwortet.

ESP -> App:

```text
hello;proto=1;fw=0.1.0;device=makropad-s3;buttons=20;sliders=3;display=ssd1306-128x64
```

App -> ESP:

```text
hello-ack;proto=1;app=0.1.0
```

Danach kann die App optional Konfigurationsdaten senden oder direkt in den
Live-Betrieb wechseln.

Aktueller App-Stand:

- Die Desktop-App antwortet automatisch auf `hello` mit
  `hello-ack;proto=1;app=0.1.0`.
- Die Desktop-App antwortet auf `ping` mit `pong`.
- Bei Funkstille sendet die Desktop-App `ping` und setzt den Status nach einem
  Timeout auf `Unresponsive`.
- Serial wird explizit mit UTF-8-Encoding betrieben.
- Bei Serial-Lesefehlern setzt die App den Verbindungsstatus auf
  `Disconnected`.

## Heartbeat

Beide Seiten duerfen Heartbeats senden. Heartbeats sind nur ein Fallback fuer
ruhige Phasen: Jede gueltige Nachricht der Gegenseite gilt ebenfalls als
Lebenszeichen.

```text
ping;id=42
pong;id=42
```

Regeln:

- Der Empfaenger beantwortet `ping` mit `pong` und gleicher `id`.
- `id` ist optional, aber empfohlen.
- Die App sendet einen `ping`, wenn laenger als ca. 2 Sekunden keine Nachricht
  vom ESP empfangen wurde.
- Wenn die App ca. 6 Sekunden keine gueltige ESP-Nachricht empfaengt, markiert
  sie die Verbindung als `Unresponsive`.
- Wenn die Firmware ca. 6 Sekunden keine gueltige App-Nachricht empfaengt, setzt
  sie den Host-Status zurueck und zeigt wieder `Waiting for app`.
- Sobald danach wieder eine gueltige Nachricht ankommt, gilt die Verbindung
  wieder als lebendig.

## ESP -> App Nachrichten

### hello

Meldet ein frisch gestartetes oder neu verbundenes Geraet.

```text
hello;proto=1;fw=0.1.0;device=makropad-s3;buttons=20;sliders=3;display=ssd1306-128x64
```

Felder:

| Key | Pflicht | Bedeutung |
| --- | --- | --- |
| `proto` | ja | Protokollversion |
| `fw` | ja | Firmware-Version |
| `device` | ja | Geraetetyp |
| `buttons` | nein | Anzahl erkannter/konfigurierter Buttons |
| `sliders` | nein | Anzahl erkannter/konfigurierter Slider |
| `display` | nein | Displaytyp |

### ready

Signalisiert, dass der ESP initialisiert ist.

```text
ready;uptime=1240
```

### button

Meldet Button-Zustandsaenderungen.

```text
button;id=1;state=down
button;id=1;state=up
```

Felder:

| Key | Pflicht | Bedeutung |
| --- | --- | --- |
| `id` | ja | 1-basierte Button-ID |
| `state` | ja | `down` oder `up` |

Firmware-Hinweis:

- Events erst nach Entprellung senden.
- Wiederholtes Senden gleicher Zustaende vermeiden.

### slider

Meldet relevante Slider-Aenderungen.

```text
slider;id=1;value=1834;raw=1841
```

Felder:

| Key | Pflicht | Bedeutung |
| --- | --- | --- |
| `id` | ja | 1-basierte Slider-ID |
| `value` | ja | geglaetteter ADC-Wert, 0 bis 4095 |
| `raw` | nein | ungefilterter ADC-Wert, 0 bis 4095 |

Firmware-Hinweis:

- Nicht jede minimale ADC-Aenderung senden.
- Fuer Version 1 reicht ein Deadband, z. B. nur senden ab 8 bis 16 ADC-Schritten
  Unterschied.

### error

Meldet Firmware- oder Hardwarefehler.

```text
error;code=display_init_failed;message=OLED%20not%20found
```

Felder:

| Key | Pflicht | Bedeutung |
| --- | --- | --- |
| `code` | ja | maschinenlesbarer Fehlercode |
| `message` | nein | lesbarer Fehlertext |

## App -> ESP Nachrichten

### hello-ack

Bestaetigt den Handshake.

```text
hello-ack;proto=1;app=0.1.0
```

### display

Aktualisiert den OLED-Inhalt.

```text
display;mode=profile;name=Gaming
display;mode=media;artist=Daft Punk;title=Digital Love
display;mode=message;text=Connected
display;mode=template;durationMs=3000;line1=Slider%201;line2=75%25
display;mode=template;durationMs=3000;line1=Track;line1Size=2;line2=Artist
display;mode=layout;layout=volume;durationMs=1500;title=Volume;line1=System;value=72
```

Unterstuetzte Modi fuer Version 1:

| Mode | Felder | Bedeutung |
| --- | --- | --- |
| `profile` | `name` | Aktives Profil anzeigen |
| `media` | `artist`, `title` | Aktuellen Track anzeigen |
| `message` | `text` | Freie Statusnachricht anzeigen |
| `template` | `durationMs`, `line1`..`line5`, optional `line1Size`..`line5Size` | App-gerenderte OLED-Zeilen temporaer anzeigen |
| `layout` | `durationMs`, `layout`, `title`, `line1`, `line2`, `value`, `icon` | Semantischen OLED-Screen mit Firmware-Primitives anzeigen |
| `clear` | keine | Display leeren |

Firmware-Hinweis:

- Lange Textzeilen werden von der Firmware auf dem OLED horizontal gescrollt.
  Die App kann daher vollstaendige Textzeilen senden; die Desktop-Preview zeigt
  nur den sichtbaren Startausschnitt.
- `lineXSize` ist optional und aktuell auf `1` oder `2` begrenzt. Ohne Feld
  rendert die Firmware die Zeile mit Groesse `1`. Groessere Zeilen belegen mehr
  Hoehe und haben entsprechend weniger sichtbare Zeichen, bevor sie scrollen.
- Die App sollte keine pixelgenauen Layoutdaten senden. `template` sendet
  bereits gerenderte Textzeilen, aber keine Bitmap- oder Browser-Daten.
- Bei `template` setzt `durationMs > 0` das Display nach Ablauf wieder auf die
  bevorzugte Anzeige zurueck. `0` zeigt den Inhalt ohne automatisches
  Zuruecksetzen.
- `layout` ist der neue bevorzugte Modus fuer visuell strukturierte Screens.
  Unterstuetzte Layouts sind aktuell `volume`, `media`, `action`, `profile`
  und `status`. Die Firmware zeichnet daraus Header, Text, einfache Icons und
  bei `volume` einen Fortschrittsbalken. `value` ist ein Prozentwert von `0`
  bis `100`; `icon` unterstuetzt `check`, `warn`, `x` und `sync`.

### config

Sendet einfache Laufzeitkonfiguration.

```text
config;profile=Default;actionResultTimeoutMs=3000;preferredDisplay=profile
config;profile=Default;defaultTitle=MakroPad;defaultTitleSize=2;defaultLine1=%7Bprofile%7D
config;profile=Default;defaultTemplateMode=html;defaultTitle=%7Bprofile%7D;defaultTitleSize=2
```

Fuer Version 1 bleibt `config` bewusst klein. Vollstaendige Hardware- und
Profilkonfigurationen bleiben zuerst in der Desktop-App.

Felder:

| Key | Pflicht | Bedeutung |
| --- | --- | --- |
| `profile` | nein | Aktives Profil fuer den Idle-Screen |
| `actionResultTimeoutMs` | nein | Dauer fuer `action-result` OLED-Meldungen in Millisekunden; `0` bedeutet ohne automatisches Zuruecksetzen |
| `preferredDisplay` | nein | `profile` oder `media`; bestimmt, wohin die Firmware nach temporaeren Action-Meldungen zurueckkehrt |
| `defaultTemplateMode` | nein | `normal` oder `html`; bei `html` rendert die App HTML-artige Defaultscreen-Felder vor dem Senden zu Textzeilen und Groessen |
| `defaultTitleSize`, `defaultLine1Size`..`defaultLine4Size` | nein | OLED-Textgroesse `1` oder `2` fuer die Defaultscreen-Zeilen; fehlt das Feld, wird `1` verwendet |

### action-result

Optionales Feedback von der App nach einer Button-Aktion.

```text
action-result;input=button:1;status=ok;message=Muted
action-result;input=button:2;status=failed;message=OBS%20not%20connected
```

Damit kann der ESP kurze Rueckmeldungen auf dem OLED anzeigen.
Die Firmware zeigt diese Meldungen temporaer an und kehrt danach zur bevorzugten
Anzeige zurueck, wenn `actionResultTimeoutMs` groesser als `0` ist. Bei aktivem
Now Playing ist das der letzte Media-Screen, sonst der Idle-/Profil-Screen.

Die Desktop-App verwendet inzwischen bevorzugt konfigurierbare
`actionResult`-Display-Trigger und sendet deren gerenderte Inhalte als
`display;mode=template`. Die `action-result` Nachricht bleibt als
Kompatibilitaets-Fallback erhalten, wenn kein passender Display-Trigger
konfiguriert ist oder das Trigger-Senden fehlschlaegt.

Now-Playing-Aenderungen werden ebenfalls bevorzugt ueber konfigurierbare
`mediaChanged`-Display-Trigger sichtbar gemacht. Die App sendet weiterhin
`display;mode=media` zur Synchronisierung von Artist/Title in der Firmware,
setzt dabei aber `preferred=false`, wenn ein aktiver Media-Trigger die sichtbare
Anzeige uebernimmt. Ohne aktiven Media-Trigger bleibt der dedizierte Media-Screen
der Fallback.

Verbindungsstatus-Aenderungen koennen appseitig ueber konfigurierbare
`connectionChanged`-Display-Trigger angezeigt werden. Da bei einem echten
Disconnect keine serielle Nachricht mehr gesendet werden kann, bleiben die
Firmware-Statusscreens fuer Offline-Zustaende der notwendige Fallback.

Profilwechsel koennen appseitig ueber `profileChanged`-Display-Trigger sichtbar
gemacht werden. Diese Trigger werden ebenfalls als `display;mode=template`
gesendet und verwenden den bereits synchronisierten `{profile}`-Wert.

Display-Trigger-Sequenzen werden in der App koordiniert. Ein neueres
Display-Ereignis bricht eine noch wartende aeltere Sequenz ab, damit veraltete
Screens nicht spaeter neuere Informationen ueberschreiben.

Aktuell sendet die App `action-result` nach ausgefuehrten Actions. Beispiele:

```text
action-result;input=button:1;status=ok;message=Play%2FPause
action-result;input=slider:1;status=ok;message=Volume%2050%25
```

## Fehlerbehandlung

Allgemeine Regeln:

- Unbekannte Nachrichtentypen ignorieren.
- Unbekannte Keys innerhalb bekannter Nachrichtentypen ignorieren.
- Fehlerhafte Zeilen ignorieren oder mit `error` beantworten.
- Protokollversionen muessen beim Handshake geprueft werden.

Beispiel fuer inkompatible Version:

```text
error;code=unsupported_protocol;message=Expected%20proto%3D1
```

## Beispielablauf

```text
ESP -> App: hello;proto=1;fw=0.1.0;device=makropad-s3;buttons=20;sliders=3;display=ssd1306-128x64
App -> ESP: hello-ack;proto=1;app=0.1.0
ESP -> App: ready;uptime=812
App -> ESP: config;profile=Default;actionResultTimeoutMs=3000;preferredDisplay=profile
ESP -> App: button;id=1;state=down
ESP -> App: button;id=1;state=up
App -> ESP: action-result;input=button:1;status=ok;message=Play%2FPause
ESP -> App: slider;id=1;value=2200
App -> ESP: display;mode=media;artist=Daft Punk;title=Digital Love
```

## Warum nicht direkt JSON?

JSON Lines waere fuer die Desktop-App bequem, ist auf dem ESP aber etwas
schwerergewichtig. Fuer den ersten Prototyp ist ein kleines Key-Value-Protokoll
leichter zu debuggen, leichter im Arduino Serial Monitor zu tippen und ohne
zusaetzliche JSON-Library nutzbar.

Wenn die Nachrichten spaeter komplexer werden, koennen wir in Version 2 auf
JSON Lines wechseln oder JSON als Value in ausgewaehlten Nachrichten erlauben.

## Offene Entscheidungen

- Sliderwerte bleiben in Version 1 als 0 bis 4095 erhalten und werden appseitig
  fuer Actions gemappt.
- Ob App-seitige Konfiguration spaeter vollstaendig an den ESP uebertragen und
  dort persistent gespeichert wird.
- Wie HID-Aktionen mit dem Serial-Protokoll koordiniert werden.
