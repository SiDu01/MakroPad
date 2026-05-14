# Hardware Plan

## Zielhardware

Dieses Dokument beschreibt den ersten Hardware-Entwurf fuer das MakroPad auf
Basis des ESP32-S3-DevKitC-1.

Geplantes Maximum:

- 20 mechanische Keyboard-Switches.
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

| Button | GPIO |
| --- | ---: |
| B01 | GPIO4 |
| B02 | GPIO5 |
| B03 | GPIO6 |
| B04 | GPIO7 |
| B05 | GPIO10 |
| B06 | GPIO11 |
| B07 | GPIO12 |
| B08 | GPIO13 |
| B09 | GPIO14 |
| B10 | GPIO15 |
| B11 | GPIO16 |
| B12 | GPIO17 |
| B13 | GPIO18 |
| B14 | GPIO21 |
| B15 | GPIO39 |
| B16 | GPIO40 |
| B17 | GPIO41 |
| B18 | GPIO42 |
| B19 | GPIO47 |
| B20 | GPIO48 |

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

Vor dem finalen Wiring sollte die konkrete Board-Revision geprueft werden.

Wenn es ein DevKitC-1 v1.1 ist:

- GPIO38 nicht fuer Buttons verwenden, weil dort die RGB LED haengt.
- GPIO48 kann fuer Button B20 verwendet werden.

Wenn es ein DevKitC-1 v1.0 ist:

- GPIO48 nicht fuer Buttons verwenden, weil dort die RGB LED haengt.
- Button B20 stattdessen auf GPIO38 legen.

Alternative, falls die Board-Revision unklar bleibt:

- B20 vorerst nicht fest verdrahten.
- Erst mit 19 Buttons testen.
- Danach mit einem kleinen Blink-/Input-Test pruefen, ob GPIO38 oder GPIO48
  unproblematischer ist.

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
