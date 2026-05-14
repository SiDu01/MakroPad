# MakroPad Project Plan

## Ziel

MakroPad soll ein selbstgebautes Makro- und Mediapad auf Basis eines
ESP32-S3-DevKitC-1 werden. Das Geraet wird zunaechst per USB mit dem PC
verbunden und soll mechanische Keyboard-Switches, analoge Slider und ein
128x64 I2C OLED Display nutzen.

Der Schwerpunkt liegt auf der Softwareentwicklung. Die Elektronik soll aber so
geplant werden, dass Firmware, PC-Anwendung und spaetere Hardware-Erweiterungen
gut zusammenpassen.

## Bekannte Anforderungen

- Ziel-Board: ESP32-S3-DevKitC-1.
- Firmware-Umgebung: Arduino IDE.
- Hauptplattform fuer die Desktop-App: Windows 11.
- Linux-Unterstuetzung ist wuenschenswert, aber Windows hat Prioritaet.
- Maximale geplante Hardware:
  - 20 Buttons mit mechanischen Keyboard-Switches, z. B. Gateron Browns.
  - 3 analoge Slider, aktuell B103 Potentiometer.
  - 1 I2C OLED Display, 128x64 px, 4 Pin, SCL/SDA.
- Die App soll eine vollstaendige Hauptansicht besitzen.
- Im Normalbetrieb soll die App im System-Tray laufen koennen.
- Die App soll Benutzerprofile und eigene Konfigurationen verwalten.
- Es soll moeglich sein, komplexere Aktionen auszufuehren.
- Das OLED soll Statusinformationen anzeigen koennen, z. B. Profil, Verbindung,
  Sliderwerte oder aktuelle Medieninformationen.
- Konkreter Displayfall: Wenn in Spotify der Track wechselt, sollen Titel und
  Kuenstler auf dem OLED angezeigt werden.

## Empfohlene Architektur

Das Projekt sollte aus Firmware und Desktop-Companion-App bestehen.

### ESP32-S3 Firmware

Aufgaben:

- Buttons lesen und entprellen.
- Slider per ADC auslesen und glaetten.
- OLED per I2C ansteuern.
- Events an die PC-App senden.
- Displaydaten von der PC-App empfangen.
- Einfache HID-Aktionen optional direkt ausfuehren, z. B. Media Keys.

Empfohlene USB-Rollen:

- USB CDC/Serial fuer Konfiguration, Events und Displaydaten.
- USB HID fuer direkte Tastatur- und Media-Control-Aktionen.

Diese Kombination ist sinnvoll, weil einfache Aktionen auch ohne aktive App
funktionieren koennen, waehrend komplexe Aktionen und dynamische Displaydaten
ueber die Companion-App laufen.

### Desktop Companion App

Empfohlene Technologie: C#/.NET mit Avalonia UI.

Gruende:

- Passt gut zu vorhandenen C#/.NET-Kenntnissen.
- Unterstuetzt Windows und Linux besser als WPF.
- Eignet sich fuer eine echte Desktop-App mit Konfigurationsoberflaeche.
- Kann im Hintergrund laufen und ein Tray-Icon anbieten.
- Serial-Kommunikation mit dem ESP32 ist gut umsetzbar.

Aufgaben:

- Geraet erkennen und verbinden.
- Profile und Konfigurationen verwalten.
- Button-, Slider- und Display-Zuordnungen bearbeiten.
- Komplexe Aktionen ausfuehren, z. B.:
  - Tastenkombinationen senden.
  - Textbausteine eingeben.
  - Programme starten.
  - Skripte ausfuehren.
  - HTTP Requests senden.
  - OBS, Discord oder andere Tools integrieren.
  - Medieninformationen auslesen und ans OLED senden.
- Im Tray weiterlaufen, auch wenn das Hauptfenster geschlossen oder minimiert
  ist.

## Hardware-Einschaetzung

20 Buttons, 3 Slider und ein I2C Display sind mit einem ESP32-S3-DevKitC-1
grundsaetzlich realistisch. Direkte GPIO-Verdrahtung ist vermutlich moeglich,
muss aber gegen das konkrete Pinout des Boards geprueft werden.

Wichtige Hinweise:

- Potentiometer an 3.3 V betreiben, nicht an 5 V.
- Slider-Wiper jeweils auf einen ADC-faehigen GPIO legen.
- OLED bevorzugt ebenfalls mit 3.3 V betreiben, sofern das Modul das erlaubt.
- I2C benoetigt zwei Pins: SDA und SCL.
- Buttons koennen gegen GND geschaltet und mit internen Pullups gelesen werden.
- Einige Pins koennen Boot-Strapping, USB, Flash/PSRAM, JTAG oder andere
  Sonderfunktionen haben. Diese Pins sollten vor dem finalen Wiring vermieden
  oder bewusst bewertet werden.

Falls direkte GPIOs knapp oder unpraktisch werden, sind zwei Alternativen
sinnvoll:

- Button-Matrix, z. B. 5x4 fuer 20 Buttons.
- I/O-Expander per I2C, z. B. MCP23017.

Fuer den ersten Prototyp ist direkte GPIO-Verdrahtung trotzdem eine gute Wahl,
weil sie Firmware und Fehlersuche einfacher macht.

## Kommunikationsmodell

Die Kommunikation zwischen Desktop-App und ESP sollte als kleines, versioniertes
Protokoll geplant werden.

Beispielhafte Nachrichten vom ESP zur App:

```text
hello;fw=0.1.0;device=makropad-s3
button;id=3;state=down
button;id=3;state=up
slider;id=1;value=742
status;ready=1
```

Beispielhafte Nachrichten von der App zum ESP:

```text
display;mode=media;artist=Daft Punk;title=Digital Love
display;mode=profile;name=Gaming
config;profile=default
ping;id=42
```

Das genaue Format ist noch offen. Fuer den Anfang ist ein einfaches
zeilenbasiertes Textprotokoll gut testbar. Spaeter kann auf JSON Lines oder ein
binaeres Protokoll gewechselt werden, falls noetig.

## Konfigurationsmodell

Die Benutzerkonfiguration sollte nicht hart auf eine feste Anzahl an Buttons
oder Slidern beschraenkt sein. Trotzdem muss die Firmware wissen, welche
physische Hardware vorhanden ist.

Sinnvolle Trennung:

- Hardware-Layout: beschreibt Pins, Button-Anzahl, Slider-Anzahl und Display.
- Benutzerprofil: beschreibt Aktionen, Namen, Icons, Displayseiten und
  App-spezifische Zuordnungen.

Beispiel:

```json
{
  "hardware": {
    "buttons": [
      { "id": 1, "pin": 4 },
      { "id": 2, "pin": 5 }
    ],
    "sliders": [
      { "id": 1, "pin": 1 }
    ],
    "display": {
      "type": "ssd1306",
      "width": 128,
      "height": 64,
      "bus": "i2c"
    }
  },
  "profiles": [
    {
      "name": "Default",
      "bindings": [
        { "input": "button:1", "action": "media.playPause" },
        { "input": "slider:1", "action": "system.volume" }
      ]
    }
  ]
}
```

## Vorgeschlagene Projektstruktur

```text
MakroPad/
  README.md
  docs/
    PROJECT_PLAN.md
    HARDWARE.md
    PROTOCOL.md
  firmware/
    MakroPadFirmware/
      MakroPadFirmware.ino
      README.md
      src/
        buttons/
        sliders/
        display/
        protocol/
        usb/
  app/
    MakroPad.App/
      MakroPad.App.csproj
    MakroPad.Core/
      MakroPad.Core.csproj
    MakroPad.Device/
      MakroPad.Device.csproj
    MakroPad.Actions/
      MakroPad.Actions.csproj
  shared/
    schemas/
      config.schema.json
  tools/
```

Geplante Rollen:

- `firmware/`: Arduino-Firmware fuer den ESP32-S3.
- `app/MakroPad.App/`: Avalonia UI, Tray, Fenster und User-Interaktion.
- `app/MakroPad.Core/`: Profile, Konfigurationen und gemeinsame Logik.
- `app/MakroPad.Device/`: Serial/HID-Kommunikation und Geraeteerkennung.
- `app/MakroPad.Actions/`: Ausfuehrung komplexer Aktionen.
- `shared/schemas/`: versionierte JSON-Schemas fuer Konfigurationen.
- `docs/`: technische Dokumentation.

Der erste Hardware-Entwurf steht in [HARDWARE.md](HARDWARE.md).
Das erste USB-Serial-Protokoll steht in [PROTOCOL.md](PROTOCOL.md).

## Empfohlener erster Meilenstein

Ein kleiner vertikaler Prototyp sollte zuerst gebaut werden:

1. ESP liest 1 Button.
2. ESP liest 1 Slider.
3. ESP schreibt Text auf das OLED.
4. ESP sendet Button- und Slider-Events per USB Serial an den PC.
5. Desktop-App erkennt den ESP.
6. Desktop-App zeigt Live-Events an.
7. Desktop-App sendet einen Displaytext zurueck an den ESP.
8. App kann in den Tray minimiert werden und weiterlaufen.

Danach koennen HID-Aktionen, Profile, Medienintegration und das volle
Hardware-Layout ausgebaut werden.

Der erste Firmware-Prototyp befindet sich in
`firmware/MakroPadFirmware/MakroPadFirmware.ino`.

Das erste Avalonia-App-Grundgeruest befindet sich in `app/MakroPad.sln`.

## Offene Entscheidungen

- Finales Pinout nach Pruefung der konkreten Board-Revision.
- Ob die Firmware das Hardware-Layout fest einkompiliert oder von der App
  empfangen und gespeichert bekommt.
- Details des Serial-Protokolls nach erstem Firmware-/App-Prototyp pruefen.
- Welche Library fuer USB HID unter Arduino verwendet wird.
- Welche Library fuer OLED verwendet wird, z. B. Adafruit SSD1306 oder U8g2.
- Wie Medieninformationen unter Windows zuerst ausgelesen werden:
  Windows Media Session API oder Spotify Web API.
- Ob Linux-Tray und Linux-Medienintegration direkt im ersten App-Design
  beruecksichtigt oder spaeter nachgezogen werden.
