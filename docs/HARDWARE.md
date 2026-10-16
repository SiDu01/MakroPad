# Hardware Plan

## Zielhardware

Dieses Dokument beschreibt den ersten Hardware-Entwurf fuer das MakroPad auf
Basis des ESP32-S3-DevKitC-1.

Finale Hardware:

- 16 mechanische Keyboard-Switches (5x4-Raster, oben rechts ersetzt das OLED
  einen 2x2-Block, siehe `hardware/case`).
- 3 analoge Slider, Typ B103 bzw. 10 kOhm Potentiometer.
- 1 OLED Display, 128x64 px, I2C, 4 Pin.
- USB-Verbindung zum PC.

Quellen:

- Espressif ESP32-S3-DevKitC-1 v1.1 User Guide:
  https://documentation.espressif.com/esp-dev-kits/en/latest/esp32s3/esp32-s3-devkitc-1/user_guide_v1.1.html
- Espressif ESP32-S3-DevKitC-1 v1.0 User Guide:
  https://documentation.espressif.com/esp-dev-kits/en/latest/esp32s3/esp32-s3-devkitc-1/user_guide_v1.0.html

## Wichtige Board-Hinweise

Das ESP32-S3-DevKitC-1 existiert in verschiedenen Varianten und Revisionen.
Besonders wichtig:

- Bei v1.1 liegt die adressierbare RGB LED laut Espressif auf GPIO38.
- Bei v1.0 liegt die adressierbare RGB LED laut Espressif auf GPIO48.
- Bei Boards mit ESP32-S3-WROOM-2 oder bestimmten Octal-SPI Flash/PSRAM
  Varianten koennen GPIO35, GPIO36 und GPIO37 intern fuer Flash/PSRAM genutzt
  sein und sollten nicht extern verwendet werden.
- GPIO19 und GPIO20 sind die nativen USB-Datenleitungen USB_D- und USB_D+.
  Diese Pins fuer das MakroPad nicht fuer Buttons, Slider oder I2C verwenden.
- GPIO0, GPIO45 und GPIO46 sind Boot-/Strapping-nahe Pins und sollten fuer die
  erste Version gemieden werden.
- GPIO43 und GPIO44 sind UART0 TX/RX. Sie bleiben am besten frei, solange wir
  sie eventuell fuer Logs, Fallback-Flashing oder Debugging nutzen wollen.

## Elektrisches Grundprinzip

### Buttons

Empfohlene Verdrahtung:

```text
GPIO ---- Switch ---- GND
```

Firmware:

- Pin als `INPUT_PULLUP` konfigurieren.
- Nicht gedrueckt: `HIGH`.
- Gedrueckt: `LOW`.
- Software-Entprellung verwenden.

Vorteile:

- Keine externen Pullup-Widerstaende noetig.
- Einfach zu verdrahten und zu testen.
- Direkte GPIO-Zuordnung ist fuer den ersten Aufbau am leichtesten zu debuggen.

### Slider

Ein B103 Slider ist typischerweise ein 10 kOhm Potentiometer.

Empfohlene Verdrahtung pro Slider:

```text
3V3 ---- Potentiometer Ende 1
GPIO --- Potentiometer Wiper / Mittelabgriff
GND ---- Potentiometer Ende 2
```

Wichtig:

- Slider nur mit 3.3 V betreiben.
- Der Wiper darf nie 5 V auf einen ESP32-S3 GPIO geben.
- Fuer Slider bevorzugt ADC1-Pins verwenden.
- Firmware-seitig Werte glaetten und kleine Aenderungen ignorieren, damit nicht
  permanent unnoetige Events gesendet werden.
- Falls ein Slider trotz Firmware-Filter sichtbar jittert: einen 100 nF
  Keramikkondensator vom Wiper/GPIO nach GND setzen, moeglichst nah am
  ESP32-Pin. Das bildet mit dem Potentiometer einen einfachen Tiefpass gegen
  ADC-Rauschen.

### OLED

Typisches 4-Pin-I2C-OLED:

```text
OLED VCC -> 3V3
OLED GND -> GND
OLED SDA -> ESP32-S3 SDA GPIO
OLED SCL -> ESP32-S3 SCL GPIO
```

Hinweise:

- Viele OLED-Module akzeptieren 3.3 V und 5 V. Fuer dieses Projekt trotzdem
  zuerst 3.3 V verwenden.
- Wahrscheinlicher Controller: SSD1306.
- Wahrscheinliche I2C-Adresse: `0x3C`, manchmal `0x3D`.

## Vorgeschlagener Pinout-Entwurf

Dieser Entwurf nimmt ein ESP32-S3-DevKitC-1 v1.1 an. Fuer v1.0 siehe Abschnitt
"Board-Revision pruefen".

### Slider

| Funktion | GPIO | Grund |
| --- | ---: | --- |
| Slider 1 | GPIO1 | ADC1_CH0, gut fuer analog |
| Slider 2 | GPIO2 | ADC1_CH1, gut fuer analog |
| Slider 3 | GPIO3 | ADC1_CH2, gut fuer analog |

### OLED I2C

| Funktion | GPIO | Hinweis |
| --- | ---: | --- |
| OLED SDA | GPIO8 | frei waehlbarer I2C-Pin |
| OLED SCL | GPIO9 | frei waehlbarer I2C-Pin |

ESP32-I2C ist flexibel. GPIO8/GPIO9 sind hier nur ein sauberer Vorschlag, damit
die ADC1-Pins fuer Slider und die USB-Pins frei bleiben.

### Buttons

Nummerierung zeilenweise von links oben (`·` = OLED):

```text
 1  2  3  ·  ·
 4  5  6  ·  ·
 7  8  9 10 11
12 13 14 15 16
```

| Button | GPIO | Button | GPIO |
| --- | ---: | --- | ---: |
| B01 | GPIO4 | B09 | GPIO14 |
| B02 | GPIO5 | B10 | GPIO15 |
| B03 | GPIO6 | B11 | GPIO16 |
| B04 | GPIO7 | B12 | GPIO17 |
| B05 | GPIO10 | B13 | GPIO18 |
| B06 | GPIO11 | B14 | GPIO21 |
| B07 | GPIO12 | B15 | GPIO39 |
| B08 | GPIO13 | B16 | GPIO40 |

Gegenüber dem 20-Tasten-Entwurf entfallen GPIO41/GPIO42 (JTAG) und
GPIO47/GPIO48 (GPIO48 = RGB-LED bei DevKitC v1.0). Sie bleiben frei für
spätere Erweiterungen. Die Pins sind in `firmware/include/config.h`
definiert.

Aktuell verdrahtet (Stand 2026-10-04): B01 an GPIO4, Slider 1 an GPIO1, OLED
an GPIO8/GPIO9.

## ESP32-S3-DevKitC-1 Pinout (Belegung MakroPad)

Beide Stiftleisten von oben gesehen, USB-Buchsen unten. Pin 1 liegt jeweils am
Antennenende (oben). Die Leistenbelegung folgt dem Espressif User Guide v1.1;
vor dem Löten mit dem Aufdruck auf der eigenen Platine abgleichen.

```text
              Antenne
      J1                    J3
 3V3   [ 1]  ●      ●  [ 1]  GND
 3V3   [ 2]  ●      ●  [ 2]  GPIO43  (UART TX, frei)
 RST   [ 3]  ●      ●  [ 3]  GPIO44  (UART RX, frei)
 B01   [ 4]  ●      ●  [ 4]  GPIO1   Slider 1
 B02   [ 5]  ●      ●  [ 5]  GPIO2   Slider 2
 B03   [ 6]  ●      ●  [ 6]  GPIO42  frei
 B04   [ 7]  ●      ●  [ 7]  GPIO41  frei
 B10   [ 8]  ●      ●  [ 8]  GPIO40  B16
 B11   [ 9]  ●      ●  [ 9]  GPIO39  B15
 B12   [10]  ●      ●  [10]  GPIO38  RGB-LED (v1.1)
 B13   [11]  ●      ●  [11]  GPIO37  meiden
 SDA   [12]  ●      ●  [12]  GPIO36  meiden
 S3    [13]  ●      ●  [13]  GPIO35  meiden
 —     [14]  ●      ●  [14]  GPIO0   Boot, meiden
 SCL   [15]  ●      ●  [15]  GPIO45  meiden
 B05   [16]  ●      ●  [16]  GPIO48  frei (RGB-LED bei v1.0)
 B06   [17]  ●      ●  [17]  GPIO47  frei
 B07   [18]  ●      ●  [18]  GPIO21  B14
 B08   [19]  ●      ●  [19]  GPIO20  USB D+
 B09   [20]  ●      ●  [20]  GPIO19  USB D−
 5V    [21]  ●      ●  [21]  GND
 GND   [22]  ●      ●  [22]  GND
           [USB-UART] [USB]
```

| J1 | GPIO | MakroPad | | J3 | GPIO | MakroPad |
| ---: | --- | --- | --- | ---: | --- | --- |
| 1 | 3V3 | Slider + OLED VCC | | 1 | GND | |
| 2 | 3V3 | | | 2 | GPIO43 | frei (UART0 TX) |
| 3 | RST | – | | 3 | GPIO44 | frei (UART0 RX) |
| 4 | GPIO4 | Taste 1 | | 4 | GPIO1 | Slider 1 |
| 5 | GPIO5 | Taste 2 | | 5 | GPIO2 | Slider 2 |
| 6 | GPIO6 | Taste 3 | | 6 | GPIO42 | frei (JTAG) |
| 7 | GPIO7 | Taste 4 | | 7 | GPIO41 | frei (JTAG) |
| 8 | GPIO15 | Taste 10 | | 8 | GPIO40 | Taste 16 |
| 9 | GPIO16 | Taste 11 | | 9 | GPIO39 | Taste 15 |
| 10 | GPIO17 | Taste 12 | | 10 | GPIO38 | RGB-LED (v1.1) |
| 11 | GPIO18 | Taste 13 | | 11 | GPIO37 | meiden (Flash/PSRAM) |
| 12 | GPIO8 | OLED SDA | | 12 | GPIO36 | meiden (Flash/PSRAM) |
| 13 | GPIO3 | Slider 3 | | 13 | GPIO35 | meiden (Flash/PSRAM) |
| 14 | GPIO46 | meiden (Strapping) | | 14 | GPIO0 | meiden (Boot) |
| 15 | GPIO9 | OLED SCL | | 15 | GPIO45 | meiden (Strapping) |
| 16 | GPIO10 | Taste 5 | | 16 | GPIO48 | frei (RGB-LED bei v1.0) |
| 17 | GPIO11 | Taste 6 | | 17 | GPIO47 | frei |
| 18 | GPIO12 | Taste 7 | | 18 | GPIO21 | Taste 14 |
| 19 | GPIO13 | Taste 8 | | 19 | GPIO20 | USB D+ |
| 20 | GPIO14 | Taste 9 | | 20 | GPIO19 | USB D− |
| 21 | 5V | – | | 21 | GND | |
| 22 | GND | Tasten, Slider, OLED GND | | 22 | GND | |

Alle Tasten schalten gegen GND (interne Pullups), die Slider liegen zwischen
3V3 und GND mit dem Schleifer am GPIO. Quelle der Zuordnung:
`firmware/include/config.h`.

## Pins, die vorerst frei bleiben sollten

| GPIO | Grund |
| ---: | --- |
| GPIO0 | Boot/Download-Funktion, fuer erste Version meiden |
| GPIO19 | USB_D-, fuer native USB-Verbindung reservieren |
| GPIO20 | USB_D+, fuer native USB-Verbindung reservieren |
| GPIO35 | eventuell intern bei bestimmten Modulen |
| GPIO36 | eventuell intern bei bestimmten Modulen |
| GPIO37 | eventuell intern bei bestimmten Modulen |
| GPIO38 | RGB LED bei DevKitC-1 v1.1 |
| GPIO43 | UART0 TX, Debug/Fallback frei halten |
| GPIO44 | UART0 RX, Debug/Fallback frei halten |
| GPIO45 | Strapping-nah, fuer erste Version meiden |
| GPIO46 | Strapping-nah, fuer erste Version meiden |

## Board-Revision pruefen

Mit 16 Tasten wird weder GPIO38 noch GPIO48 benötigt. Die Revision ist nur
noch für die RGB-LED relevant (v1.1: GPIO38, v1.0: GPIO48).

## Erster Prototyp

Fuer den ersten Firmware- und App-Test reicht ein kleiner Aufbau:

| Funktion | GPIO |
| --- | ---: |
| Button 1 | GPIO4 |
| Slider 1 | GPIO1 |
| OLED SDA | GPIO8 |
| OLED SCL | GPIO9 |

Damit koennen wir den kompletten Datenfluss testen:

1. Button druecken.
2. ESP sendet Button-Event an die PC-App.
3. Slider bewegen.
4. ESP sendet Slider-Event an die PC-App.
5. PC-App sendet Displaytext an den ESP.
6. ESP zeigt Text auf dem OLED an.

## Offene Hardware-Aufgaben

- Konkrete Board-Revision und Modulvariante ablesen.
- Pruefen, ob das OLED-Modul bei 3.3 V stabil arbeitet.
- I2C-Adresse des OLED per Scanner bestimmen.
- Slider mechanisch so planen, dass GND/3V3-Richtung zur gewuenschten
  Bedienlogik passt.
- Entscheiden, ob spaeter eine Platine, Lochraster oder Handverdrahtung genutzt
  wird.
